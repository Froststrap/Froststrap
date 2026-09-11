// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Media.Imaging;
using System.Collections.ObjectModel;

namespace Froststrap.Models;

internal class ServerInfo
{
    [JsonPropertyName("jobId")]
    public string JobId { get; set; } = "";

    [JsonPropertyName("region")]
    public string Region { get; set; } = "";

    [JsonPropertyName("joinedAt")]
    public DateTime JoinedAt { get; set; }

    [JsonPropertyName("isLatest")]
    public bool IsLatest { get; set; }

    [JsonPropertyName("playing")]
    public int Playing { get; set; }

    [JsonPropertyName("maxPlayers")]
    public int MaxPlayers { get; set; }

    [JsonIgnore]
    public string PlayerCount => $"{Playing}/{MaxPlayers}";

    [JsonPropertyName("uptime")]
    public string Uptime { get; set; } = "";

    [JsonPropertyName("serverType")]
    public ServerType ServerType { get; set; } = ServerType.Public;

    [JsonPropertyName("timeLeft")]
    public DateTime? TimeLeft { get; set; }

    [JsonIgnore]
    public ObservableCollection<Bitmap> PlayerAvatarThumbnails { get; } = [];

    [JsonIgnore]
    public Collection<string> PlayerTokens { get; } = [];

    [JsonIgnore]
    public string ExtraPlayersText { get; set; } = "";

    [JsonIgnore]
    public bool HasExtraPlayers { get; set; }

    public string DurationText
    {
        get
        {
            if (JoinedAt == default) return "";
            string start = JoinedAt.ToString("HH:mm", CultureInfo.InvariantCulture);
            string end = TimeLeft.HasValue
                ? TimeLeft.Value.ToString("HH:mm", CultureInfo.InvariantCulture)
                : "…";
            return $"From {start} to {end}";
        }
    }
}