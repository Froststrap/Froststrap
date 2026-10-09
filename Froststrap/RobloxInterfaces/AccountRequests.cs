using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Froststrap.RobloxInterfaces
{
    internal static class AccountRequests
    {
        public static async Task SendAsync(HttpMethod method, Uri url, object body, string setting, string value)
        {
            using var response = await SendJsonAsync(method, url, body);
            await EnsureAcceptedAsync(response, setting, value);
        }

        public static async Task DeleteAsync(Uri url)
        {
            using var response = await SendJsonAsync(HttpMethod.Delete, url, null);

            response.EnsureSuccessStatusCode();
        }

        public static async Task<T> PostJsonAsync<T>(Uri url, object body)
        {
            using var response = await SendJsonAsync(HttpMethod.Post, url, body);
            response.EnsureSuccessStatusCode();
            return JsonSerializer.Deserialize<T>(await response.Content.ReadAsStringAsync())!;
        }

        private static Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, Uri url, object? body) =>
            App.Cookies.AuthRequest(new HttpRequestMessage(method, url)
            {
                Content = body is null ? null : new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            });

        private static async Task EnsureAcceptedAsync(HttpResponseMessage response, string setting, string value)
        {
            if (response.IsSuccessStatusCode)
                return;

            string body = await response.Content.ReadAsStringAsync();
            string? reason = null;

            try
            {
                using var doc = JsonDocument.Parse(body);

                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("errors", out JsonElement errors)
                    && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0
                    && errors[0].ValueKind == JsonValueKind.Object
                    && errors[0].TryGetProperty("message", out JsonElement msg)
                    && msg.ValueKind == JsonValueKind.String)
                {
                    reason = msg.GetString();
                }
            }
            catch (JsonException) { }

            if (String.IsNullOrWhiteSpace(reason))
                reason = String.IsNullOrWhiteSpace(body) ? null : body.Length > 200 ? body[..200] : body;

            throw new SettingRejectedException(setting, value, (int)response.StatusCode, reason);
        }
    }
}