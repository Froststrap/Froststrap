using System.ComponentModel;
using System.Text.Json;

namespace Froststrap.RobloxInterfaces
{
    // i am 100% sure there is a much, MUCH better way to handle this
    // matt wrote this so this is effectively a black box to me right now
    // i'll likely refactor this at some point
    public class ApplicationSettings
    {
        private readonly string _applicationName;
        private readonly string _channelName;

        private bool _initialised = false;
        private Dictionary<string, string>? _flags;

        private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);

        private ApplicationSettings(string applicationName, string channelName)
        {
            _applicationName = applicationName;
            _channelName = channelName;
        }

        private async Task Fetch()
        {
            if (_initialised)
                return;

            await _semaphoreSlim.WaitAsync();
            try
            {
                if (_initialised)
                    return;

                string logIndent = $"ApplicationSettings::Fetch.{_applicationName}.{_channelName}";
                App.Logger.WriteLine(logIndent, "Fetching fast flags");

                string path = $"/v2/settings/application/{_applicationName}";

                if (!string.Equals(_channelName, Deployment.DefaultChannel, StringComparison.OrdinalIgnoreCase))
                    path += $"/bucket/{_channelName}";

                HttpResponseMessage response;

                try
                {
                    response = await App.HttpClient.GetAsync("https://clientsettingscdn." + Deployment.RobloxDomain + path);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(logIndent, "Failed to contact clientsettingscdn! Falling back to clientsettings...");
                    App.Logger.WriteException(logIndent, ex);

                    response = await App.HttpClient.GetAsync("https://clientsettings." + Deployment.RobloxDomain + path);
                }

                string rawResponse = await response.Content.ReadAsStringAsync();
                response.EnsureSuccessStatusCode();

                var clientSettings = JsonSerializer.Deserialize<ClientFlagSettings>(rawResponse)
                    ?? throw new Exception("Deserialised client settings is null!");

                _flags = clientSettings.ApplicationSettings
                    ?? throw new Exception("Deserialised application settings is null!");

                _initialised = true;
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }

        public async Task<T?> GetAsync<T>(string name)
        {
            await Fetch();

            if (_flags == null || !_flags.TryGetValue(name, out string? value))
                return default;

            try
            {
                var converter = TypeDescriptor.GetConverter(typeof(T));
                return (T?)converter.ConvertFromString(value);
            }
            catch (NotSupportedException)
            {
                return default;
            }
        }

        public T? Get<T>(string name)
        {
            return GetAsync<T>(name).GetAwaiter().GetResult();
        }

        // _cache[applicationName][channelName]
        private static readonly Dictionary<string, Dictionary<string, ApplicationSettings>> _cache = [];

        public static ApplicationSettings PCDesktopClient => GetSettings("PCDesktopClient");

        public static ApplicationSettings PCClientBootstrapper => GetSettings("PCClientBootstrapper");

        public static ApplicationSettings GetSettings(string applicationName, string channelName = Deployment.DefaultChannel, bool shouldCache = true)
        {
            channelName = channelName.ToLowerInvariant();

            lock (_cache)
            {
                if (_cache.TryGetValue(applicationName, out var channelCache) && channelCache.TryGetValue(channelName, out var settings))
                    return settings;

                var flags = new ApplicationSettings(applicationName, channelName);

                if (shouldCache)
                {
                    if (!_cache.TryGetValue(applicationName, out channelCache))
                    {
                        channelCache = [];
                        _cache[applicationName] = channelCache;
                    }

                    channelCache[channelName] = flags;
                }

                return flags;
            }
        }
    }
}