using Froststrap.Models.APIs.RobloxParty;
using Froststrap.Models.APIs.RobloxParty.Events;
using Froststrap;
using Froststrap.Integrations.OverlayModules;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Froststrap.Integrations.OverlayModules
{
    internal class RobloxParty
    {
        private const string ApiService = "apis";
        private const string ApiPath = "platform-chat-api/v1";

        private readonly RealtimeMessaging? _messaging;

        public event EventHandler<MessageEvent>? IncomingMessage;

        public RobloxParty(RealtimeMessaging? messaging)
        {
            if (messaging is null)
                return;

            _messaging = messaging;
            _messaging.PartyChat += OnIncomingMessage;
        }

        private void OnIncomingMessage(object? sender, MessageEvent message) =>
            IncomingMessage?.Invoke(this, message);

        public async Task<ConversationsPage?> GetConversations(int pageSize = 20, string? cursor = null)
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

        public async Task<UserMessagesPage?> GetMessages(Conversation conversation, string? cursor = null)
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

        public async Task SendMessage(string conversationId, string messageContent)
        {
            var payload = new MessagesContents
            {
                ConversationId = conversationId,
                Messages = new[] { new MessageContent { Content = messageContent } }
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            string csrf = await App.Cookies.GetXCSRF();

            using var response = await App.Cookies.AuthPost(
                UrlBuilder.BuildApiUrl(ApiService, $"{ApiPath}/send-messages"), content, csrf);

            response.EnsureSuccessStatusCode();

            var result = JsonSerializer.Deserialize<UserMessagesPage>(await response.Content.ReadAsStringAsync());

            if (result is null)
                return;

            foreach (UserMessage message in result.Messages)
            {
                if (message.Status != "moderated")
                    continue;

                App.Logger.Warn("Message was moderated");

                throw new InvalidOperationException("Message was moderated by the platform.");
            }
        }

        public async Task UpdateTypingStatus(Conversation conversation)
        {
            var payload = new ConversationPayload { Id = conversation.Id };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            try
            {
                string csrf = await App.Cookies.GetXCSRF();

                using var response = await App.Cookies.AuthPost(
                    UrlBuilder.BuildApiUrl(ApiService, $"{ApiPath}/update-typing-status"), content, csrf);
            }
            catch (HttpRequestException ex)
            {
                App.Logger.Error("Failed to update typing status");
                App.Logger.Error(ex);
            }
        }
    }
}