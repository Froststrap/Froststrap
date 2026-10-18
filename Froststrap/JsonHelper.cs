// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace Froststrap
{
    internal static class JsonHelper
    {
        internal static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            TypeInfoResolver = BuildResolver()
        };

        private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = BuildResolver()
        };

        private static readonly JsonSerializerOptions PlainOptions = new()
        {
            TypeInfoResolver = BuildResolver()
        };

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Reflection-based resolution is intentional and verified; hot startup types are covered by the source-generated JsonContext.")]
        private static IJsonTypeInfoResolver BuildResolver()
            => JsonTypeInfoResolver.Combine(JsonContext.Default, new DefaultJsonTypeInfoResolver());

        internal static JsonTypeInfo<T> TypeInfo<T>(JsonSerializerOptions? options = null)
            => (JsonTypeInfo<T>)(options ?? Options).GetTypeInfo(typeof(T))!;

        internal static string Serialize<T>(T value, JsonSerializerOptions? options = null)
            => JsonSerializer.Serialize(value, TypeInfo<T>(options));

        internal static T? Deserialize<T>(string json, JsonSerializerOptions? options = null)
            => JsonSerializer.Deserialize(json, TypeInfo<T>(options));

        internal static T? Deserialize<T>(ReadOnlySpan<byte> utf8Json, JsonSerializerOptions? options = null)
            => JsonSerializer.Deserialize(utf8Json, TypeInfo<T>(options));

        internal static T? Deserialize<T>(JsonElement element, JsonSerializerOptions? options = null)
            => element.Deserialize(TypeInfo<T>(options ?? PlainOptions));

        internal static JsonNode? SerializeToNode<T>(T value, JsonSerializerOptions? options = null)
            => JsonSerializer.SerializeToNode(value, TypeInfo<T>(options));

        internal static JsonElement SerializeToElement<T>(T value, JsonSerializerOptions? options = null)
            => JsonSerializer.SerializeToElement(value, TypeInfo<T>(options));

        internal static ValueTask<T?> DeserializeAsync<T>(Stream stream, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default)
            => JsonSerializer.DeserializeAsync<T>(stream, TypeInfo<T>(options), cancellationToken);

        internal static Task SerializeAsync<T>(Stream stream, T value, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default)
            => JsonSerializer.SerializeAsync(stream, value, TypeInfo<T>(options), cancellationToken);

        internal static Task<T?> ReadFromJsonAsync<T>(HttpContent content, CancellationToken cancellationToken = default)
            => content.ReadFromJsonAsync(TypeInfo<T>(WebOptions), cancellationToken);

        internal static Task<T?> GetFromJsonAsync<T>(HttpClient client, string? requestUri, CancellationToken cancellationToken = default)
            => client.GetFromJsonAsync(requestUri, TypeInfo<T>(WebOptions), cancellationToken);
    }
}
