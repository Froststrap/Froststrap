using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Enums.Overlay;
using Froststrap.Integrations.OverlayModules;
using Froststrap.Models.APIs.RobloxParty;
using Froststrap.Models.APIs.RobloxParty.Events;
using Froststrap.Models.Overlay;
using Froststrap.RobloxInterfaces;
using Froststrap.Utility;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Overlay.Controls
{
    internal class FriendActivityViewModel : NotifyPropertyChangedViewModel
    {
        private const int DetailsBatch = 100;
        private const string OneToOne = "one_to_one";

        private static readonly TimeSpan GroupGap = TimeSpan.FromMinutes(5);

        private readonly Integrations.Overlay? _overlay;
        private readonly RobloxParty? _party;
        private readonly FriendPresence? _presence;

        private readonly Dictionary<long, ChatFriend> _people = [];
        private readonly List<ChatFriend> _groups = [];
        private readonly Dictionary<FriendSection, FriendListHeader> _headers;

        private AuthenticatedUser? _me;
        private string? _myAvatar;
        private string? _listStatus;

        private bool _loading;
        private bool _loaded;
        private bool _requested;

        public ObservableCollection<object> Rows { get; } = [];

        public ObservableCollection<ChatTab> Tabs { get; } = [];

        public event EventHandler? MessagesAdded;

        private ChatTab? _selectedTab;
        public ChatTab? SelectedTab
        {
            get => _selectedTab;
            private set
            {
                if (_selectedTab == value)
                    return;

                if (_selectedTab is not null)
                    _selectedTab.IsSelected = false;

                _selectedTab = value;

                if (value is not null)
                {
                    value.IsSelected = true;
                    value.Friend.Unread = 0;
                }

                OnPropertyChanged(nameof(SelectedTab));
                OnPropertyChanged(nameof(HasTab));
                OnPropertyChanged(nameof(ShowPlaceholder));
            }
        }

        public bool HasTab => _selectedTab is not null;

        public bool ShowPlaceholder => _selectedTab is null;

        private string _searchText = String.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value)
                    return;

                _searchText = value;

                OnPropertyChanged(nameof(SearchText));

                Rebuild();
            }
        }

        public string MyName { get; private set; } = String.Empty;

        public string MyHandle { get; private set; } = String.Empty;

        public string? MyAvatar => _myAvatar;

        public string ListStatus => _listStatus ?? String.Empty;

        public bool ShowListStatus => !String.IsNullOrEmpty(_listStatus);

        public ICommand OpenChatCommand { get; }
        public ICommand SelectTabCommand { get; }
        public ICommand CloseTabCommand { get; }
        public ICommand ToggleSectionCommand { get; }
        public ICommand SendCommand { get; }
        public ICommand JoinCommand { get; }

        public FriendActivityViewModel(Integrations.Overlay? overlay)
        {
            _overlay = overlay;
            _party = overlay?.Messaging.Party;
            _presence = overlay?.Friends;

            _headers = new Dictionary<FriendSection, FriendListHeader>
            {
                [FriendSection.InGame] = new() { Section = FriendSection.InGame, Title = Strings.Menu_Overlay_Messages_SectionInGame },
                [FriendSection.Online] = new() { Section = FriendSection.Online, Title = Strings.Menu_Overlay_Messages_SectionOnline },
                [FriendSection.Offline] = new() { Section = FriendSection.Offline, Title = Strings.Menu_Overlay_Messages_SectionOffline },
                [FriendSection.Groups] = new() { Section = FriendSection.Groups, Title = Strings.Menu_Overlay_Messages_SectionGroups }
            };

            OpenChatCommand = new AsyncRelayCommand<ChatFriend>(OpenChatAsync);
            SelectTabCommand = new RelayCommand<ChatTab>(tab => SelectedTab = tab);
            CloseTabCommand = new RelayCommand<ChatTab>(CloseTab);
            ToggleSectionCommand = new RelayCommand<FriendListHeader>(ToggleSection);
            SendCommand = new AsyncRelayCommand(SendAsync, AsyncRelayCommandOptions.AllowConcurrentExecutions);
            JoinCommand = new RelayCommand(Join);

            if (_party is not null)
                _party.IncomingMessage += OnIncomingMessage;

            if (_presence is not null)
                _presence.Updated += (_, _) => Dispatcher.UIThread.Post(ApplyPresence);

            App.Cookies.WatchAccount(this, static vm => vm.OnAccountChanged());
        }

        private void OnAccountChanged() => Dispatcher.UIThread.Post(() =>
        {
            _me = null;
            _myAvatar = null;
            _loaded = false;

            _people.Clear();
            _groups.Clear();

            Tabs.Clear();
            Rows.Clear();

            SelectedTab = null;

            MyName = String.Empty;
            MyHandle = String.Empty;

            OnPropertyChanged(nameof(MyName));
            OnPropertyChanged(nameof(MyHandle));
            OnPropertyChanged(nameof(MyAvatar));

            SetListStatus(null);

            if (_requested)
                _ = LoadAsync(true);
        });

        public async Task LoadAsync(bool force = false)
        {
            _requested = true;

            if (_loading || (_loaded && !force))
                return;

            if (!App.Settings.Prop.AllowCookieAccess)
            {
                SetListStatus(Strings.Menu_Overlay_Messages_NeedsCookies);
                return;
            }

            _loading = true;

            if (_people.Count == 0)
                SetListStatus(Strings.Menu_Overlay_Messages_Loading);

            try
            {
                if (!App.Cookies.Loaded)
                    await Task.Run(App.Cookies.LoadCookies);

                _me = App.Cookies.CurrentUser;

                if (_me is null)
                {
                    SetListStatus(Strings.Menu_Overlay_Messages_NeedsCookies);
                    return;
                }

                _overlay?.WatchFriendsForPanel();

                if (_presence is not null && !_presence.HasLooked)
                    await _presence.PollAsync();

                List<Conversation> conversations = _party is null ? new() : await RobloxParty.GetAllConversations();

                var ids = new HashSet<long>(_presence?.FriendIds ?? []);

                foreach (Conversation conversation in conversations.Where(IsOneToOne))
                {
                    long other = conversation.Participants.FirstOrDefault(x => x != _me.Id);

                    if (other > 0)
                        ids.Add(other);
                }

                ids.Add(_me.Id);

                var details = await FetchDetailsAsync([.. ids]);

                details.TryGetValue(_me.Id, out UserDetails? mine);

                _myAvatar = mine?.Thumbnail?.ImageUrl;

                MyName = mine?.Data.DisplayName ?? _me.DisplayName;
                MyHandle = $"@{mine?.Data.Name ?? _me.Username}";

                OnPropertyChanged(nameof(MyName));
                OnPropertyChanged(nameof(MyHandle));
                OnPropertyChanged(nameof(MyAvatar));

                ids.Remove(_me.Id);

                foreach (long id in ids)
                {
                    if (_people.ContainsKey(id))
                        continue;

                    details.TryGetValue(id, out UserDetails? person);

                    _people[id] = new ChatFriend
                    {
                        UserId = id,
                        DisplayName = person?.Data.DisplayName ?? person?.Data.Name ?? id.ToString(Locale.CurrentCulture),
                        Username = person?.Data.Name ?? String.Empty,
                        Headshot = person?.Thumbnail?.ImageUrl
                    };
                }

                _groups.Clear();

                foreach (Conversation conversation in conversations)
                {
                    if (IsOneToOne(conversation))
                    {
                        long other = conversation.Participants.FirstOrDefault(x => x != _me.Id);

                        if (_people.TryGetValue(other, out ChatFriend? friend))
                        {
                            friend.ConversationId = conversation.Id;
                            friend.Unread = conversation.UnreadMessagesCount;
                        }

                        continue;
                    }

                    _groups.Add(new ChatFriend
                    {
                        IsGroup = true,
                        DisplayName = String.IsNullOrWhiteSpace(conversation.Name) ? Strings.Menu_Overlay_Messages_UnnamedGroup : conversation.Name,
                        MemberCount = conversation.Participants.Length,
                        ConversationId = conversation.Id,
                        Unread = conversation.UnreadMessagesCount
                    });
                }

                _loaded = true;

                SetListStatus(null);

                ApplyPresence();
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to load friends and conversations");
                App.Logger.Error(ex);

                if (_people.Count == 0)
                    SetListStatus(Strings.Menu_Overlay_Messages_LoadFailed);
            }
            finally
            {
                _loading = false;
            }
        }

        private static bool IsOneToOne(Conversation conversation) =>
            conversation.Type == OneToOne || conversation.Participants.Length <= 2;

        private static async Task<Dictionary<long, UserDetails>> FetchDetailsAsync(List<long> ids)
        {
            var details = new Dictionary<long, UserDetails>();

            foreach (long[] batch in ids.Chunk(DetailsBatch))
            {
                foreach (var (id, user) in await UserDetails.FetchBatch(batch.ToList()))
                    details[id] = user;
            }

            return details;
        }

        private void ApplyPresence()
        {
            IReadOnlyDictionary<long, UserPresence> presences = _presence?.Presences ?? new Dictionary<long, UserPresence>();

            foreach ((long id, ChatFriend friend) in _people)
                friend.Update(presences.GetValueOrDefault(id));

            Rebuild();
        }

        private void Rebuild()
        {
            string search = _searchText.Trim();

            bool Shown(ChatFriend friend) => search.Length == 0 || friend.Matches(search);

            var sections = new (FriendSection Section, List<ChatFriend> People)[]
            {
                (FriendSection.InGame, [.. _people.Values.Where(x => x.IsInGame && Shown(x))]),
                (FriendSection.Online, [.. _people.Values.Where(x => (x.IsOnline || x.IsInStudio) && Shown(x))]),
                (FriendSection.Offline, [.. _people.Values.Where(x => x.Status == FriendStatus.Offline && Shown(x))]),
                (FriendSection.Groups, [.. _groups.Where(Shown)])
            };

            var rows = new List<object>();

            foreach ((FriendSection section, List<ChatFriend> people) in sections)
            {
                if (people.Count == 0)
                    continue;

                FriendListHeader header = _headers[section];
                header.Count = people.Count;

                rows.Add(header);

                if (header.IsExpanded)
                    rows.AddRange(people.OrderBy(x => x.DisplayName, StringComparer.CurrentCultureIgnoreCase));
            }

            if (!rows.SequenceEqual(Rows))
            {
                Rows.Clear();

                foreach (object row in rows)
                    Rows.Add(row);
            }

            if (!_loaded)
                return;

            if (rows.Count > 0)
                SetListStatus(null);
            else if (search.Length > 0)
                SetListStatus(String.Format(Locale.CurrentCulture, Strings.Menu_Overlay_Messages_NoMatches, search));
            else
                SetListStatus(Strings.Menu_Overlay_Messages_Empty);
        }

        private void ToggleSection(FriendListHeader? header)
        {
            if (header is null)
                return;

            header.IsExpanded = !header.IsExpanded;

            Rebuild();
        }

        private void SetListStatus(string? status)
        {
            if (_listStatus == status)
                return;

            _listStatus = status;

            OnPropertyChanged(nameof(ListStatus));
            OnPropertyChanged(nameof(ShowListStatus));
        }

        private async Task OpenChatAsync(ChatFriend? friend)
        {
            if (friend is null)
                return;

            ChatTab? tab = Tabs.FirstOrDefault(x => x.Friend == friend);

            if (tab is null)
            {
                tab = new ChatTab { Friend = friend };
                Tabs.Add(tab);
            }

            SelectedTab = tab;

            if (!tab.Loaded && !tab.IsLoading)
                await RefreshTabAsync(tab);
        }

        private void CloseTab(ChatTab? tab)
        {
            if (tab is null)
                return;

            int index = Tabs.IndexOf(tab);

            Tabs.Remove(tab);

            if (_selectedTab == tab)
                SelectedTab = Tabs.Count == 0 ? null : Tabs[Math.Clamp(index, 0, Tabs.Count - 1)];
        }

        private async Task RefreshTabAsync(ChatTab tab)
        {
            if (_party is null || String.IsNullOrEmpty(tab.Friend.ConversationId))
            {
                tab.Loaded = true;
                return;
            }

            tab.IsLoading = !tab.Loaded;

            try
            {
                UserMessagesPage? page = await RobloxParty.GetMessages(new Conversation { Id = tab.Friend.ConversationId });

                if (page is null)
                    return;

                bool added = false;

                foreach (UserMessage message in Enumerable.Reverse(page.Messages))
                {
                    if (String.IsNullOrEmpty(message.Id) || !tab.KnownMessages.Add(message.Id))
                        continue;

                    if (!String.IsNullOrEmpty(message.Visibility) && message.Visibility != "visible")
                        continue;

                    long sender = message.Sender ?? UserMessage.SystemSenderId;

                    if (sender == UserMessage.SystemSenderId)
                        AddSystemLine(tab, message.Content, message.CreatedAt);
                    else
                        AddLine(tab, sender, message.Content, message.CreatedAt).MessageId = message.Id;

                    added = true;
                }

                tab.Loaded = true;

                if (added)
                    MessagesAdded?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to load a conversation");
                App.Logger.Error(ex);
            }
            finally
            {
                tab.IsLoading = false;
            }
        }

        private static void AddSystemLine(ChatTab tab, string text, DateTime? at)
        {
            AddDivider(tab, at);

            tab.Rows.Add(new ChatSystemLine(text));
        }

        private ChatLine AddLine(ChatTab tab, long sender, string text, DateTime? at)
        {
            bool mine = _me is not null && sender == _me.Id;

            AddDivider(tab, at);

            var group = tab.Rows.LastOrDefault() as ChatMessageGroup;

            bool continues = group is not null
                && group.SenderId == sender
                && (at is null || group.LastAt is null || at.Value - group.LastAt.Value <= GroupGap);

            if (!continues)
            {
                ChatFriend? author = mine ? null : _people.GetValueOrDefault(sender);

                group = new ChatMessageGroup
                {
                    SenderId = sender,
                    IsMine = mine,
                    Sender = mine ? Strings.Menu_Overlay_Messages_You : author?.DisplayName ?? tab.Friend.DisplayName,
                    Avatar = mine ? _myAvatar : author?.Headshot ?? (tab.Friend.IsGroup ? null : tab.Friend.Headshot),
                    StartedAt = at
                };

                tab.Rows.Add(group);
            }

            group!.LastAt = at ?? group.LastAt;

            var line = new ChatLine { Text = text };

            group.Lines.Add(line);

            return line;
        }

        private static void AddDivider(ChatTab tab, DateTime? at)
        {
            if (at is not DateTime time)
                return;

            DateTime day = time.ToLocalTime().Date;

            if (tab.Rows.OfType<ChatDayDivider>().LastOrDefault()?.Day == day)
                return;

            tab.Rows.Add(new ChatDayDivider(day, DayText(day)));
        }

        private static string DayText(DateTime day)
        {
            if (day == DateTime.Today)
                return Strings.Menu_Overlay_Messages_Today;

            if (day == DateTime.Today.AddDays(-1))
                return Strings.Menu_Overlay_Messages_Yesterday;

            return day.ToString("D", Locale.CurrentCulture);
        }

        private async Task SendAsync()
        {
            ChatTab? tab = _selectedTab;

            if (tab is null || _party is null || _me is null || !tab.CanSend)
                return;

            string text = tab.Draft.Trim();

            tab.Draft = String.Empty;

            ChatLine line = AddLine(tab, _me.Id, text, DateTime.UtcNow);
            line.State = ChatLineState.Pending;

            MessagesAdded?.Invoke(this, EventArgs.Empty);

            try
            {
                if (String.IsNullOrEmpty(tab.Friend.ConversationId))
                {
                    Conversation? created = tab.Friend.IsGroup ? null : await RobloxParty.CreateConversation(tab.Friend.UserId);

                    if (created is null)
                    {
                        Fail(line, String.Format(Locale.CurrentCulture, Strings.Menu_Overlay_Messages_CouldntStart, tab.Friend.DisplayName));
                        return;
                    }

                    tab.Friend.ConversationId = created.Id;
                }

                UserMessage? sent = await RobloxParty.SendMessage(tab.Friend.ConversationId!, text);

                if (!String.IsNullOrEmpty(sent?.Id))
                {
                    line.MessageId = sent.Id;
                    tab.KnownMessages.Add(sent.Id);
                }

                line.State = ChatLineState.Sent;
            }
            catch (MessageModeratedException)
            {
                Fail(line, Strings.Menu_Overlay_Messages_Moderated);
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to send a message");
                App.Logger.Error(ex);

                Fail(line, Strings.Menu_Overlay_Messages_NotSent);
            }
        }

        private static void Fail(ChatLine line, string reason)
        {
            line.Failure = reason;
            line.State = ChatLineState.Failed;
        }

        private void OnIncomingMessage(object? sender, MessageEvent message)
        {
            if (message.IsTyping is not null)
                return;

            _ = Dispatcher.UIThread.InvokeAsync(async () =>
            {
                ChatTab? tab = Tabs.FirstOrDefault(x => x.Friend.ConversationId == message.ConversationId);

                if (tab is not null)
                {
                    await RefreshTabAsync(tab);

                    if (tab != _selectedTab)
                        tab.Friend.Unread++;

                    return;
                }

                ChatFriend? owner = _people.Values.Concat(_groups).FirstOrDefault(x => x.ConversationId == message.ConversationId);

                if (owner is not null)
                    owner.Unread++;
                else
                    await LoadAsync(true);
            });
        }

        private void Join()
        {
            ChatFriend? friend = _selectedTab?.Friend;

            if (friend is null)
                return;

            try
            {
                if (friend.CanJoin)
                {
                    App.Logger.Info("Joining a friend from their chat");

                    GameServers.Join(friend.PlaceId, friend.ServerId!);
                }
                else if (friend.CanViewGame)
                {
                    long place = friend.RootPlaceId > 0 ? friend.RootPlaceId : friend.PlaceId;

                    Threading.ShellExecute($"https://www.{Deployment.RobloxDomain}/games/{place}");
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to follow a friend");
                App.Logger.Error(ex);
            }
        }
    }
}