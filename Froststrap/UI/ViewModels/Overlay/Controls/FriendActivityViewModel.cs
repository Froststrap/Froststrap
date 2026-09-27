using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Integrations.OverlayModules;
using Froststrap.Models.APIs.RobloxParty;
using Froststrap.Models.APIs.RobloxParty.Events;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Froststrap.UI.ViewModels.Overlay.Controls
{
    internal class FriendActivityViewModel : NotifyPropertyChangedViewModel
    {
        private readonly RobloxParty? _party;
        private readonly List<FriendItem> _allFriends = [];

        private AuthenticatedUser? _current;
        private FriendItem? _selected;

        public ObservableCollection<ChatMessage> MessagesList { get; } = [];

        public ObservableCollection<FriendItem> FriendsList { get; } = [];

        private string _searchText = String.Empty;

        public string SearchTextBoxContent
        {
            get => _searchText;
            set
            {
                if (_searchText == value)
                    return;

                _searchText = value;

                OnPropertyChanged(nameof(SearchTextBoxContent));

                ApplyFriendFilter();
            }
        }

        private string _messageText = String.Empty;

        public string MessageTextBoxContent
        {
            get => _messageText;
            set
            {
                if (_messageText == value)
                    return;

                _messageText = value;

                OnPropertyChanged(nameof(MessageTextBoxContent));
            }
        }

        public bool ShowEmptyState => FriendsList.Count == 0;

        public bool ShowConversationPlaceholder => _selected is null;

        public ICommand SendMessageCommand { get; }

        public FriendActivityViewModel(RobloxParty? party)
        {
            _party = party;

            SendMessageCommand = new AsyncRelayCommand(SendMessage);

            _party?.IncomingMessage += OnIncomingMessage;
        }

        private void ApplyFriendFilter()
        {
            FriendsList.Clear();

            IEnumerable<FriendItem> source = String.IsNullOrEmpty(_searchText)
                ? _allFriends
                : _allFriends.Where(f => f.Username.Contains(_searchText, StringComparison.OrdinalIgnoreCase));

            foreach (FriendItem friend in source)
                FriendsList.Add(friend);

            OnPropertyChanged(nameof(ShowEmptyState));
        }

        private void OnIncomingMessage(object? sender, MessageEvent message)
        {
            if (_selected is null || _selected.ConversationId != message.ConversationId)
                return;

            _ = Dispatcher.UIThread.InvokeAsync(async () => await RefreshConversation(_selected));
        }

        public async Task LoadConversations()
        {
            if (_party is null || !App.Settings.Prop.AllowCookieAccess)
                return;

            try
            {
                if (!App.Cookies.Loaded)
                    await Task.Run(App.Cookies.LoadCookies);

                _current = App.Cookies.CurrentUser;

                if (_current is null)
                    return;

                ConversationsPage? page = await RobloxParty.GetConversations();

                if (page is null)
                    return;

                var participants = page.Conversations
                    .SelectMany(x => x.Participants)
                    .Distinct()
                    .ToList();

                var users = await UserDetails.FetchBatch(participants);

                _allFriends.Clear();

                foreach (Conversation conversation in page.Conversations)
                {
                    long otherUserId = conversation.Participants.FirstOrDefault(x => x != _current.Id);

                    users.TryGetValue(otherUserId, out UserDetails? details);

                    _allFriends.Add(new FriendItem
                    {
                        Username = conversation.Name,
                        StatusText = details?.Data.Name is null ? String.Empty : $"@{details.Data.Name}",
                        ProfileImage = details?.Thumbnail.ImageUrl,
                        ConversationId = conversation.Id,
                        Data = await RobloxParty.GetMessages(conversation)
                    });
                }

                ApplyFriendFilter();
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to load conversations");
                App.Logger.Error(ex);
            }
        }

        public Task LoadConversationHistory(FriendItem conversation)
        {
            _selected = conversation;

            MessagesList.Clear();

            OnPropertyChanged(nameof(ShowConversationPlaceholder));

            if (conversation.Data?.Messages is null)
            {
                App.Logger.Info($"No history for {conversation.Username}");
                return Task.CompletedTask;
            }

            App.Logger.Info($"Loading conversation for {conversation.Username}");

            foreach (UserMessage message in conversation.Data.Messages)
                AddMessage(message);

            return Task.CompletedTask;
        }

        private async Task RefreshConversation(FriendItem conversation)
        {
            if (_party is null)
                return;

            conversation.Data = await RobloxParty.GetMessages(new Conversation { Id = conversation.ConversationId });

            await LoadConversationHistory(conversation);
        }

        private void AddMessage(UserMessage message)
        {
            if (_selected is null || _current is null)
                return;

            long sender = message.Sender ?? UserMessage.SystemSenderId;

            if (sender == UserMessage.SystemSenderId)
                return;

            if (MessagesList.Any(x => x.MessageId == message.Id))
                return;

            bool isCurrentUser = sender == _current.Id;

            MessagesList.Insert(0, new ChatMessage
            {
                Text = message.Content,
                Sender = isCurrentUser ? "You" : _selected.Username,
                IsCurrentUser = isCurrentUser,
                MessageId = message.Id
            });
        }

        public async Task SendMessage()
        {
            if (_party is null || _selected is null || String.IsNullOrWhiteSpace(_messageText))
                return;

            var pending = new ChatMessage
            {
                Text = _messageText,
                Sender = "You",
                IsCurrentUser = true,
                State = ChatMessageState.Pending
            };

            string outgoing = _messageText;

            MessageTextBoxContent = String.Empty;
            MessagesList.Add(pending);

            try
            {
                await RobloxParty.SendMessage(_selected.ConversationId, outgoing);

                pending.State = ChatMessageState.Sent;
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to send message to {_selected.ConversationId}");
                App.Logger.Error(ex);

                pending.State = ChatMessageState.Failed;
            }
        }
    }
}