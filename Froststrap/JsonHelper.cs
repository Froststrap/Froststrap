// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace Froststrap
{
    internal static partial class JsonHelper
    {
        private static readonly Lock ResolverLock = new();

        /// <summary>
        /// Source-generated metadata for the types declared in <see cref="JsonContext"/>.
        /// Reflection-based resolution is intentionally not used so this stays Native AOT safe.
        /// </summary>
        internal static readonly IJsonTypeInfoResolver Resolver = BuildResolver();

        private static JsonContext BuildResolver()
        {
            return JsonContext.Default;
        }

        internal static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            TypeInfoResolver = Resolver
        };

        private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web)
        {
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            TypeInfoResolver = Resolver
        };

        internal static readonly JsonSerializerOptions PlainOptions = new()
        {
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            TypeInfoResolver = Resolver
        };

        private static JsonSerializerOptions Prepare(JsonSerializerOptions options)
        {
            if (options.TypeInfoResolver == null)
            {
                lock (ResolverLock)
                {
                    options.TypeInfoResolver ??= Resolver;
                }
            }

            return options;
        }

        internal static JsonTypeInfo<T> TypeInfo<T>(JsonSerializerOptions? options = null)
            => Prepare(options ?? Options).GetTypeInfo(typeof(T)) as JsonTypeInfo<T>
                ?? throw new InvalidOperationException(
                    $"No source-generated JSON metadata is registered for {typeof(T)}. " +
                    "Add it to JsonContext.cs.");

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
