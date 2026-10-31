// SPDX-FileCopyrightText: 2026 Froststrap
// SPDX-License-Identifier: MPL-2.0

using System.Net;
using System.Net.Http;
using System.Reflection;

namespace Froststrap.Localisation;

public class RemoteLocalisationServiceTests
{
    public static bool RunAll()
    {
        bool ok = true;
        try
        {
            var tmp = Path.Combine(Path.GetTempPath(), "FroststrapTest_" + Guid.NewGuid());
            Directory.CreateDirectory(tmp);
            RemoteLocalisationService.LoadCached("de-DE");
            var cacheFile = Path.Combine(Paths.Cache, "LocalisationCache", "de-DE.json");
            if (File.Exists(cacheFile)) ok = true;
            else ok = false;
            Directory.Delete(tmp, true);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Test error: " + ex.Message); ok = false; }
        return ok;
    }

    private class FakeHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _manifest;
        private readonly HttpResponseMessage _content;
        public FakeHandler(HttpResponseMessage manifest, HttpResponseMessage content)
        {
            _manifest = manifest;
            _content = content;
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsoluteUri.Contains("manifest")) return Task.FromResult(_manifest);
            return Task.FromResult(_content);
        }
    }
}
