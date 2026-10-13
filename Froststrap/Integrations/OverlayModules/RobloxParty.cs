﻿﻿using Froststrap.Models.APIs.RealtimeMessaging;
using Froststrap.Models.APIs.RobloxParty;
using Froststrap.Models.APIs.RobloxParty.Events;
using Froststrap.Utility;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Froststrap.Integrations.OverlayModules
{
    internal class RobloxParty
    {
        private const string PlatformChatService = "platform-chat-api";
        private const int ConversationPageSize = 100;

        public event EventHandler<MessageEvent>? IncomingMessage;
        public event EventHandler<ChannelMarkedRead>? ChannelRead;
        public event EventHandler<ChannelMetadataUpdate>? ChannelMetadataChanged;

        public RobloxParty(RealtimeMessaging? messaging)
        {
            if (messaging is null)
                return;

            messaging.PartyChat += OnIncomingMessage;
            messaging.ChannelRead += OnChannelRead;
            messaging.ChannelMetadataChanged += OnChannelMetadataChanged;
        }

        private void OnIncomingMessage(object? sender, MessageEvent message) =>
            IncomingMessage?.Invoke(this, message);

        private void OnChannelRead(object? sender, ChannelMarkedRead note) =>
            ChannelRead?.Invoke(this, note);

        private void OnChannelMetadataChanged(object? sender, ChannelMetadataUpdate note) =>
            ChannelMetadataChanged?.Invoke(this, note);

        public static async Task<ConversationsPage?> GetConversations(int pageSize = ConversationPageSize, string? cursor = null)
        {
            Uri url = UrlBuilder.BuildApisUrl(
                $"{PlatformChatService}/v1/get-user-conversations?pageSize={pageSize}&include_user_data=false&cursor={cursor}");

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

        public static async Task<List<Conversation>> GetAllConversations()
        {
            ConversationsPage? page = await GetConversations();

            return page?.Conversations ?? [];
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

        public static async Task<UserMessagesPage?> GetMessages(string conversationId, string? cursor = null)
        {
            if (String.IsNullOrEmpty(conversationId))
                return null;

            Uri url = UrlBuilder.BuildApisUrl(
                $"{PlatformChatService}/v1/get-conversation-messages?conversation_id={conversationId}&cursor={cursor}");

            try
            {
                return await Http.AuthGetJson<UserMessagesPage>(url);
            }
            catch (Exception ex) when (ex is JsonException || ex is HttpRequestException)
            {
                App.Logger.Error($"Failed to get messages for {conversationId}");
                App.Logger.Error(ex);
            }

            return null;
        }

        public static async Task<UserMessage?> SendMessage(string conversationId, string messageContent)
        {
            var payload = new MessagesContents
            {
                ConversationId = conversationId,
                Messages = [new MessageContent { Content = messageContent }]
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

        public static async Task UpdateTypingStatus(string conversationId)
        {
            if (String.IsNullOrEmpty(conversationId))
                return;

            try
            {
                using var response = await PostAsync("update-typing-status", new ConversationPayload { Id = conversationId });
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

            return await App.Cookies.AuthPost(
                UrlBuilder.BuildApisUrl($"{PlatformChatService}/v1/{endpoint}"), content);
        }

        public static async Task MarkAsRead(params string[] conversationIds)
        {
            var ids = conversationIds
                .Where(x => !String.IsNullOrEmpty(x))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (ids.Length == 0)
                return;

            try
            {
                using var response = await PostAsync("mark-conversations",
                    new { conversation_ids = ids });

                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                App.Logger.Error($"Failed to mark {ids.Length} conversation(s) as read");
                App.Logger.Error(ex);
            }
        }
    }
}