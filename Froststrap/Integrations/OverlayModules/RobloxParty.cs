using Froststrap.Models.APIs.RobloxParty;
using Froststrap.Models.APIs.RobloxParty.Events;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Froststrap.Integrations.OverlayModules
{
    internal class RobloxParty
    {
        private const string ApiService = "apis";
        private const string ApiPath = "platform-chat-api/v1";

        public event EventHandler<MessageEvent>? IncomingMessage;

        public RobloxParty(RealtimeMessaging? messaging)
        {
            if (messaging is null)
                return;

            messaging.PartyChat += OnIncomingMessage;
        }

        private void OnIncomingMessage(object? sender, MessageEvent message) =>
            IncomingMessage?.Invoke(this, message);

        public static async Task<ConversationsPage?> GetConversations(int pageSize = 20, string? cursor = null)
        {
            Uri url = UrlBuilder.BuildApiUrl(ApiService,
                $"{ApiPath}/get-user-conversations?pageSize={pageSize}&include_user_data=true&cursor={cursor}");

            try
            {
                return await Http.AuthGetJson<ConversationsPage>(url);
            }
            catch (Exception ex) when (ex is JsonException || ex is HttpRequestException)
            {
                App.Logger.Error("Failed to get conversations");
                App.Logger.Error(ex);
            }

            return null;
        }

        public static async Task<List<Conversation>> GetAllConversations(int maxPages = 5)
        {
            var conversations = new List<Conversation>();
            string? cursor = null;

            for (int page = 0; page < maxPages; page++)
            {
                ConversationsPage? result = await GetConversations(50, cursor);

                if (result is null)
                    break;

                conversations.AddRange(result.Conversations);

                cursor = result.NextCursor;

                if (String.IsNullOrEmpty(cursor))
                    break;
            }

            return conversations;
        }

        public static async Task<Conversation?> CreateConversation(long userId)
        {
            var payload = new
            {
                conversations = new[] { new { type = "one_to_one", participant_user_ids = new[] { userId } } },
                include_user_data = false
            };

            try
            {
                using var response = await PostAsync("create-conversations", payload);
                response.EnsureSuccessStatusCode();

                var page = JsonSerializer.Deserialize<ConversationsPage>(await response.Content.ReadAsStringAsync());

                return page?.Conversations.FirstOrDefault(x => !String.IsNullOrEmpty(x.Id));
            }
            catch (Exception ex) when (ex is JsonException || ex is HttpRequestException)
            {
                App.Logger.Error("Failed to start a conversation");
                App.Logger.Error(ex);
            }

            return null;
        }

        public static async Task<UserMessagesPage?> GetMessages(Conversation conversation, string? cursor = null)
        {
            if (String.IsNullOrEmpty(conversation.Id))
                return null;

            Uri url = UrlBuilder.BuildApiUrl(ApiService,
                $"{ApiPath}/get-conversation-messages?conversation_id={conversation.Id}&cursor={cursor}");

            try
            {
                return await Http.AuthGetJson<UserMessagesPage>(url);
            }
            catch (Exception ex) when (ex is JsonException || ex is HttpRequestException)
            {
                App.Logger.Error($"Failed to get messages for {conversation.Id}");
                App.Logger.Error(ex);
            }

            return null;
        }

        public static async Task<UserMessage?> SendMessage(string conversationId, string messageContent)
        {
            var payload = new MessagesContents
            {
                ConversationId = conversationId,
                Messages = [ new MessageContent { Content = messageContent } ]
            };

            using var response = await PostAsync("send-messages", payload);
            response.EnsureSuccessStatusCode();

            var result = JsonSerializer.Deserialize<UserMessagesPage>(await response.Content.ReadAsStringAsync());

            if (result is null)
                return null;

            if (result.Messages.Any(x => x.Status == "moderated"))
            {
                App.Logger.Warn("Message was moderated");

                throw new MessageModeratedException();
            }

            return result.Messages.FirstOrDefault();
        }

        public static async Task UpdateTypingStatus(Conversation conversation)
        {
            try
            {
                using var response = await PostAsync("update-typing-status", new ConversationPayload { Id = conversation.Id });
            }
            catch (HttpRequestException ex)
            {
                App.Logger.Error("Failed to update typing status");
                App.Logger.Error(ex);
            }
        }

        private static async Task<HttpResponseMessage> PostAsync(string endpoint, object payload)
        {
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            string csrf = await App.Cookies.GetXCSRF();

            return await App.Cookies.AuthPost(UrlBuilder.BuildApiUrl(ApiService, $"{ApiPath}/{endpoint}"), content, csrf);
        }
    }
}