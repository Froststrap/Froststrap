// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.APIs.RealtimeMessaging
{
    internal sealed class FriendshipNotification
    {
        public const string CreatedType   = "FriendshipCreated";
        public const string DestroyedType = "FriendshipDestroyed";
        public const string RequestedType = "FriendshipRequested";

        [JsonPropertyName("Type")]
        public string? Type { get; set; }

        [JsonPropertyName("EventArgs")]
        public FriendshipEventArgs? EventArgs { get; set; }

        [JsonPropertyName("SequenceNumber")]
        public long SequenceNumber { get; set; }

        [JsonPropertyName("RealtimeMessageIdentifier")]
        public string? RealtimeMessageIdentifier { get; set; }
    }
}