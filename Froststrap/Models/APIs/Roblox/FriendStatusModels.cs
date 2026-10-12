// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Text.Json.Serialization;

namespace Froststrap.Models.APIs.Roblox
{
    internal enum FriendStatus
    {
        Unknown,
        NotFriends,
        Friends,
        RequestSent,
        RequestReceived
    }

    internal class FriendStatusResponse
    {
        [JsonPropertyName("data")]
        public List<FriendStatusEntry> Data { get; set; } = [];
    }

    internal class FriendStatusEntry
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = String.Empty;
    }

    internal static class FriendStatusExtensions
    {
        public static FriendStatus Parse(string? value) => value switch
        {
            "Friends" => FriendStatus.Friends,
            "NotFriends" => FriendStatus.NotFriends,
            "RequestSent" => FriendStatus.RequestSent,
            "RequestReceived" => FriendStatus.RequestReceived,
            _ => FriendStatus.Unknown
        };
    }
}