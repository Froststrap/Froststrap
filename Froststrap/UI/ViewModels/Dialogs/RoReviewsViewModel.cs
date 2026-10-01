// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Collections.ObjectModel;
using System.Net.Http.Json;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Integrations.AccountManager;

namespace Froststrap.UI.ViewModels.Dialogs;

internal sealed class RoReviewsViewModel : NotifyPropertyChangedViewModel, IDisposable
{
    private const string ApiBase = "https://hermivore.cat";
    private const int MaxLength = 8000;
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(5);
    private readonly long _targetId;
    private readonly CancellationTokenSource _cancellation = new();
    private int _refreshInProgress;
    private readonly AvatarService _avatarService = new();
    private string? _sessionToken;
    private long? _userId;
    private string _draft = string.Empty;
    private bool _isLoading;
    private bool _disposed;

    public string GameName { get; }
    public ObservableCollection<RoReviewItemViewModel> Reviews { get; } = [];
    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(_sessionToken);
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public string Draft
    {
        get => _draft;
        set
        {
            value = value[..Math.Min(value.Length, MaxLength)];
            if (SetProperty(ref _draft, value))
            {
                OnPropertyChanged(nameof(CharacterCount));
                SubmitCommand?.NotifyCanExecuteChanged();
            }
        }
    }
    public int CharacterCount => Draft.Length;

    public IAsyncRelayCommand AuthorizeCommand { get; }
    public IAsyncRelayCommand SubmitCommand { get; }
    public IRelayCommand SignOutCommand { get; }

    public RoReviewsViewModel(long targetId, string gameName)
    {
        _targetId = targetId;
        GameName = gameName;
        _sessionToken = AccountSecurity.GetCredential("RoReviews");
        if (long.TryParse(AccountSecurity.GetCredential("RoReviewsUserId"), out long userId))
            _userId = userId;
        else if (_sessionToken is not null && TryGetJwtUserId(_sessionToken, out long tokenUserId))
            _userId = tokenUserId;
        AuthorizeCommand = new AsyncRelayCommand(AuthorizeAsync);
        SubmitCommand = new AsyncRelayCommand(SubmitAsync, () => IsAuthenticated && !string.IsNullOrWhiteSpace(Draft));
        SignOutCommand = new RelayCommand(SignOut);
        _ = LoadReviewsAsync();
        _ = RefreshReviewsAsync();
    }

    private async Task RefreshReviewsAsync()
    {
        try
        {
            while (!_cancellation.IsCancellationRequested)
            {
                await Task.Delay(RefreshInterval, _cancellation.Token);
                await LoadReviewsAsync(showLoading: false);
            }
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested) { }
    }

    private async Task AuthorizeAsync()
    {
        IsLoading = true;
        try
        {
            using var challenge = await SendAsync(HttpMethod.Post, "/api/roblox/oauth/challenge", null, includeAuth: false);
            var challengeData = await challenge.Content.ReadFromJsonAsync<JsonElement>(_cancellation.Token);
            string? authUrl = GetString(challengeData, "auth_url");
            string? sessionId = GetString(challengeData, "session_id");
            if (authUrl == null || sessionId == null) throw new InvalidOperationException(Strings.RoReviews_AuthorizationFailed);

            Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });
            while (!_cancellation.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), _cancellation.Token);
                using var statusResponse = await SendAsync(HttpMethod.Get, $"/api/roblox/oauth/status/{Uri.EscapeDataString(sessionId)}", null, includeAuth: false);
                var status = await statusResponse.Content.ReadFromJsonAsync<JsonElement>(_cancellation.Token);
                if (string.Equals(GetString(status, "status"), "ok", StringComparison.OrdinalIgnoreCase))
                {
                    _sessionToken = GetString(status, "session_token");
                    if (string.IsNullOrWhiteSpace(_sessionToken))
                        throw new InvalidOperationException(Strings.RoReviews_AuthorizationFailed);

                    AccountSecurity.SetCredential("RoReviews", _sessionToken);
                    if (GetLong(status, "user_id") is long authenticatedUserId)
                    {
                        _userId = authenticatedUserId;
                        AccountSecurity.SetCredential("RoReviewsUserId", authenticatedUserId.ToString(CultureInfo.InvariantCulture));
                    }
                    OnPropertyChanged(nameof(IsAuthenticated));
                    SubmitCommand.NotifyCanExecuteChanged();
                    await LoadReviewsAsync();
                    return;
                }
                if (string.Equals(GetString(status, "status"), "expired", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(Strings.RoReviews_AuthorizationExpired);
            }
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested) { }
        catch (Exception ex) { App.Logger.Error($"RoReviews authorization failed: {ex.Message}"); }
        finally { IsLoading = false; }
    }

    private async Task LoadReviewsAsync(bool showLoading = true)
    {
        if (Interlocked.Exchange(ref _refreshInProgress, 1) != 0)
            return;

        try
        {
            if (showLoading)
                IsLoading = true;
            using var response = await SendAsync(HttpMethod.Get, $"/api/roblox/reviews/{_targetId}?game=true", null, includeAuth: true);
            var root = await response.Content.ReadFromJsonAsync<JsonElement>(_cancellation.Token);
            if (IsSessionError(root))
            {
                ResetAuthentication();
                return;
            }
            if (root.TryGetProperty("reviews", out var reviews) && reviews.ValueKind == JsonValueKind.Array)
            {
                var userIds = reviews.EnumerateArray()
                    .SelectMany(review => EnumerateReviewUsers(review))
                    .Distinct()
                    .ToList();
                var avatars = await _avatarService.GetAvatarUrlsBulkAsync(userIds);
                var incoming = new List<RoReviewItemViewModel>();
                foreach (var review in reviews.EnumerateArray())
                    incoming.Add(ParseReview(review, null, avatars));
                ApplyReviews(incoming);
            }
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested) { }
        catch (Exception ex) { App.Logger.Error($"RoReviews load failed: {ex.Message}"); }
        finally
        {
            if (showLoading)
                IsLoading = false;
            Volatile.Write(ref _refreshInProgress, 0);
        }
    }

    private void ApplyReviews(List<RoReviewItemViewModel> incoming)
        {
            var incomingIds = incoming.Select(review => review.Id).ToHashSet(StringComparer.Ordinal);

            for (int index = Reviews.Count - 1; index >= 0; index--)
            {
                if (!incomingIds.Contains(Reviews[index].Id))
                    Reviews.RemoveAt(index);
            }

            for (int index = 0; index < incoming.Count; index++)
            {
                var next = incoming[index];
                int existingIndex = Reviews
                    .Select((review, itemIndex) => (review, itemIndex))
                    .FirstOrDefault(item => string.Equals(item.review.Id, next.Id, StringComparison.Ordinal))
                    .itemIndex;

                if (existingIndex < Reviews.Count && string.Equals(Reviews[existingIndex].Id, next.Id, StringComparison.Ordinal))
                {
                    Reviews[existingIndex].UpdateFrom(next);
                    if (existingIndex != index)
                        Reviews.Move(existingIndex, index);
                }
                else
                {
                    Reviews.Insert(index, next);
            }
        }
    }

    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(Draft)) return;
        string content = Draft.Trim();
        Draft = string.Empty;
        try
        {
            await SendAsync(HttpMethod.Post, $"/api/roblox/reviews/{_targetId}?game=true",
                JsonContent.Create(new { content }), includeAuth: true);
            await LoadReviewsAsync();
        }
        catch (Exception ex) { App.Logger.Error($"RoReviews submit failed: {ex.Message}"); Draft = content; }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, HttpContent? content, bool includeAuth)
    {
        var request = new HttpRequestMessage(method, new Uri(ApiBase + path)) { Content = content };
        if (includeAuth && !string.IsNullOrWhiteSpace(_sessionToken))
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _sessionToken);
        var response = await App.HttpClient.SendAsync(request, _cancellation.Token);
        if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            ResetAuthentication();
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"RoReviews request failed: {(int)response.StatusCode}");
        return response;
    }

    private RoReviewItemViewModel ParseReview(JsonElement value, string? parentReviewId, IReadOnlyDictionary<long, string?> avatars)
    {
        long? userId = GetLong(value, "from", "id");
        var item = new RoReviewItemViewModel(
            GetString(value, "id") ?? Guid.NewGuid().ToString("N"),
            GetAuthorName(value),
            GetString(value, "content") ?? string.Empty,
            GetTimestamp(value),
            value.TryGetProperty("edited", out var edited) && edited.ValueKind == JsonValueKind.True,
            GetScore(value, "up"), GetScore(value, "down"), parentReviewId,
            GetRatingUsers(value, "up"), GetRatingUsers(value, "down"),
            userId.HasValue && avatars.TryGetValue(userId.Value, out var avatarUrl) ? avatarUrl : null);
        if (value.TryGetProperty("replies", out var replies) && replies.ValueKind == JsonValueKind.Array)
            foreach (var reply in replies.EnumerateArray())
                item.Replies.Add(ParseReview(reply, item.Id, avatars));
        item.VoteAsync = vote => RateAsync(item, vote);
        return item;
    }

    private static IEnumerable<long> EnumerateReviewUsers(JsonElement review)
    {
        long? userId = GetLong(review, "from", "id");
        if (userId.HasValue)
            yield return userId.Value;

        if (review.TryGetProperty("replies", out var replies) && replies.ValueKind == JsonValueKind.Array)
        {
            foreach (var reply in replies.EnumerateArray())
            {
                foreach (var replyUserId in EnumerateReviewUsers(reply))
                    yield return replyUserId;
            }
        }
    }

    private async Task RateAsync(RoReviewItemViewModel item, string vote)
    {
        if (_sessionToken == null || !_userId.HasValue) return;
        int up = item.Upvotes, down = item.Downvotes;
        var previousUp = item.UpvoterIds.ToHashSet();
        var previousDown = item.DownvoterIds.ToHashSet();
        if (vote == "up")
        {
            if (item.UpvoterIds.Remove(_userId.Value))
                item.Upvotes--;
            else
            {
                item.UpvoterIds.Add(_userId.Value);
                item.Upvotes++;
                if (item.DownvoterIds.Remove(_userId.Value))
                    item.Downvotes--;
            }
        }
        else
        {
            if (item.DownvoterIds.Remove(_userId.Value))
                item.Downvotes--;
            else
            {
                item.DownvoterIds.Add(_userId.Value);
                item.Downvotes++;
                if (item.UpvoterIds.Remove(_userId.Value))
                    item.Upvotes--;
            }
        }
        try
        {
            string path = item.ParentReviewId == null
                ? $"/api/roblox/reviews/{_targetId}/{item.Id}/rate?game=true"
                : $"/api/roblox/reviews/{_targetId}/{item.ParentReviewId}/reply/{item.Id}/rate?game=true";
            await SendAsync(HttpMethod.Post, path,
                JsonContent.Create(new { vote }), includeAuth: true);
        }
        catch
        {
            item.Upvotes = up;
            item.Downvotes = down;
            item.UpvoterIds = previousUp;
            item.DownvoterIds = previousDown;
        }
    }

    private static string GetAuthorName(JsonElement value)
        => value.TryGetProperty("from", out var from) && from.TryGetProperty("name", out var name)
            ? name.GetString() ?? Strings.RoReviews_UnknownAuthor
            : Strings.RoReviews_UnknownAuthor;

    private static int GetScore(JsonElement value, string key)
        => value.TryGetProperty("score", out var score) && score.TryGetProperty(key, out var count) && count.TryGetInt32(out int result)
            ? result : 0;

    private static HashSet<long> GetRatingUsers(JsonElement value, string key)
    {
        var ids = new HashSet<long>();
        if (value.TryGetProperty("rating", out var rating)
            && rating.TryGetProperty(key, out var users)
            && users.ValueKind == JsonValueKind.Array)
        {
            foreach (var user in users.EnumerateArray())
                if (user.TryGetInt64(out long userId))
                    ids.Add(userId);
        }
        return ids;
    }

    private static string? GetString(JsonElement value, string key)
        => value.TryGetProperty(key, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;

    private static string GetTimestamp(JsonElement value)
    {
        string? rawTimestamp = GetString(value, "time") ?? GetString(value, "timestamp");
        if (DateTimeOffset.TryParse(rawTimestamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            return parsed.ToLocalTime().ToString("d", CultureInfo.CurrentCulture);

        JsonElement? timestamp = null;
        if (value.TryGetProperty("time", out var time))
            timestamp = time;
        else if (value.TryGetProperty("timestamp", out var timestampProperty))
            timestamp = timestampProperty;

        if (timestamp is { ValueKind: JsonValueKind.Number } numeric
            && numeric.TryGetDouble(out double unixTime))
        {
            if (unixTime > 10_000_000_000)
                unixTime /= 1000;

            return DateTimeOffset.FromUnixTimeSeconds((long)unixTime)
                .ToLocalTime()
                .ToString("d", CultureInfo.CurrentCulture);
        }

        return rawTimestamp ?? string.Empty;
    }

    private static long? GetLong(JsonElement value, string key)
        => value.TryGetProperty(key, out var property) && TryParseLong(property, out long result)
            ? result
            : null;

    private static bool TryParseLong(JsonElement value, out long result)
    {
        if (value.ValueKind == JsonValueKind.Number)
            return value.TryGetInt64(out result);

        if (value.ValueKind == JsonValueKind.String)
            return long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

        result = 0;
        return false;
    }

    private static bool TryGetJwtUserId(string token, out long userId)
    {
        userId = 0;
        string[] parts = token.Split('.');
        if (parts.Length < 2)
            return false;

        try
        {
            string payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var document = JsonDocument.Parse(Convert.FromBase64String(payload));
            foreach (string key in new[] { "user_id", "userId", "sub", "id" })
            {
                if (document.RootElement.TryGetProperty(key, out var value)
                    && TryParseLong(value, out userId))
                    return true;
            }
        }
        catch (FormatException) { }
        catch (JsonException) { }
        catch (InvalidOperationException) { }

        return false;
    }

    private static long? GetLong(JsonElement value, string objectKey, string propertyKey)
        => value.TryGetProperty(objectKey, out var parent)
            && parent.TryGetProperty(propertyKey, out var property)
            && TryParseLong(property, out var result)
                ? result
                : null;

    private static bool IsSessionError(JsonElement value)
        => value.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.True
            && (string.Equals(GetString(value, "reason"), "Validation Required", StringComparison.OrdinalIgnoreCase)
                || string.Equals(GetString(value, "reason"), "Invalid JWT", StringComparison.OrdinalIgnoreCase));

    private void ResetAuthentication()
    {
        _sessionToken = null;
        _userId = null;
        AccountSecurity.DeleteCredential("RoReviews");
        AccountSecurity.DeleteCredential("RoReviewsUserId");
        OnPropertyChanged(nameof(IsAuthenticated));
        SubmitCommand.NotifyCanExecuteChanged();
    }

    private void SignOut()
    {
        AccountSecurity.DeleteCredential("RoReviews");
        _sessionToken = null;
        _userId = null;
        Reviews.Clear();
        AccountSecurity.DeleteCredential("RoReviewsUserId");
        OnPropertyChanged(nameof(IsAuthenticated));
        SubmitCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cancellation.Cancel();
        _cancellation.Dispose();
    }
}

internal sealed class RoReviewItemViewModel : NotifyPropertyChangedViewModel
{
    private int _upvotes;
    private int _downvotes;
    public string Id { get; }
    public string AuthorName { get; private set; }
    public string? AvatarUrl { get; private set; }
    public string DisplayContent { get; private set; }
    public string Timestamp { get; private set; }
    public string? ParentReviewId { get; }
    public bool IsEdited { get; private set; }
    public ObservableCollection<RoReviewItemViewModel> Replies { get; } = [];
    public int Upvotes { get => _upvotes; set => SetProperty(ref _upvotes, value); }
    public int Downvotes { get => _downvotes; set => SetProperty(ref _downvotes, value); }
    public HashSet<long> UpvoterIds { get; set; }
    public HashSet<long> DownvoterIds { get; set; }
    public Func<string, Task>? VoteAsync { private get; set; }
    public IAsyncRelayCommand UpvoteCommand { get; }
    public IAsyncRelayCommand DownvoteCommand { get; }

    public RoReviewItemViewModel(string id, string authorName, string content, string timestamp, bool edited, int upvotes, int downvotes, string? parentReviewId, HashSet<long> upvoterIds, HashSet<long> downvoterIds, string? avatarUrl)
    {
        Id = id; AuthorName = authorName; DisplayContent = content; Timestamp = timestamp; IsEdited = edited; ParentReviewId = parentReviewId; AvatarUrl = avatarUrl;
        _upvotes = upvotes; _downvotes = downvotes;
        UpvoterIds = upvoterIds;
        DownvoterIds = downvoterIds;
        UpvoteCommand = new AsyncRelayCommand(() => VoteAsync?.Invoke("up") ?? Task.CompletedTask);
        DownvoteCommand = new AsyncRelayCommand(() => VoteAsync?.Invoke("down") ?? Task.CompletedTask);
    }

    public void UpdateFrom(RoReviewItemViewModel incoming)
    {
        if (!string.Equals(AuthorName, incoming.AuthorName, StringComparison.Ordinal))
        {
            AuthorName = incoming.AuthorName;
            OnPropertyChanged(nameof(AuthorName));
        }

        if (!string.Equals(DisplayContent, incoming.DisplayContent, StringComparison.Ordinal))
        {
            DisplayContent = incoming.DisplayContent;
            OnPropertyChanged(nameof(DisplayContent));
        }

        if (!string.Equals(Timestamp, incoming.Timestamp, StringComparison.Ordinal))
        {
            Timestamp = incoming.Timestamp;
            OnPropertyChanged(nameof(Timestamp));
        }

        if (IsEdited != incoming.IsEdited)
        {
            IsEdited = incoming.IsEdited;
            OnPropertyChanged(nameof(IsEdited));
        }

        if (!string.Equals(AvatarUrl, incoming.AvatarUrl, StringComparison.Ordinal))
        {
            AvatarUrl = incoming.AvatarUrl;
            OnPropertyChanged(nameof(AvatarUrl));
        }

        Upvotes = incoming.Upvotes;
        Downvotes = incoming.Downvotes;
        UpvoterIds = incoming.UpvoterIds;
        DownvoterIds = incoming.DownvoterIds;
        ApplyReplies(incoming.Replies);
    }

    private void ApplyReplies(ObservableCollection<RoReviewItemViewModel> incoming)
    {
        var incomingIds = incoming.Select(reply => reply.Id).ToHashSet(StringComparer.Ordinal);
        for (int index = Replies.Count - 1; index >= 0; index--)
        {
            if (!incomingIds.Contains(Replies[index].Id))
                Replies.RemoveAt(index);
        }

        for (int index = 0; index < incoming.Count; index++)
        {
            var next = incoming[index];
            int existingIndex = Replies
                .Select((reply, replyIndex) => (reply, replyIndex))
                .FirstOrDefault(item => string.Equals(item.reply.Id, next.Id, StringComparison.Ordinal))
                .replyIndex;

            if (existingIndex < Replies.Count && string.Equals(Replies[existingIndex].Id, next.Id, StringComparison.Ordinal))
            {
                Replies[existingIndex].UpdateFrom(next);
                if (existingIndex != index)
                    Replies.Move(existingIndex, index);
            }
            else
            {
                Replies.Insert(index, next);
            }
        }
    }
}
