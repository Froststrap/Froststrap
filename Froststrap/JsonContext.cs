// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Text.Json.Serialization;
using Froststrap.Models.APIs.Config;
using Froststrap.Models.Persistable;

namespace Froststrap
{
    [JsonSourceGenerationOptions(WriteIndented = true, ReadCommentHandling = JsonCommentHandling.Skip)]
    [JsonSerializable(typeof(Settings))]
    [JsonSerializable(typeof(State))]
    [JsonSerializable(typeof(OverlayNotes))]
    [JsonSerializable(typeof(DistributionState))]
    [JsonSerializable(typeof(RobloxState))]
    [JsonSerializable(typeof(RemoteDataBase))]
    [JsonSerializable(typeof(Dictionary<string, object>))]
    internal partial class JsonContext : JsonSerializerContext
    {
    }
}
