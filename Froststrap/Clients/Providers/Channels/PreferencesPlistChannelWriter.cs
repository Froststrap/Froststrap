// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Xml;
using System.Xml.Linq;
using Froststrap.Clients.Contracts;

namespace Froststrap.Clients.Providers.Channels;

internal sealed class PreferencesPlistChannelWriter(string registryName) : IChannelWriter
{
    public void Write(string robloxDomain, string? channel)
    {
        string preferences = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Preferences");
        string path = Path.Combine(preferences, $"com.roblox.{registryName}Channel.plist");
        string key = "www." + robloxDomain;

        try
        {
            XDocument document = File.Exists(path) ? XDocument.Load(path) : NewPlist();

            if (document.Root?.Element("dict") is not { } dict)
            {
                App.Logger.Warn($"'{path}' is not a plist dictionary, leaving the channel unchanged");
                return;
            }

            var entry = dict.Elements("key").FirstOrDefault(element => element.Value == key);

            if (channel is null)
            {
                if (entry is null)
                    return;

                (entry.ElementsAfterSelf().FirstOrDefault())?.Remove();
                entry.Remove();
            }
            else if (entry?.ElementsAfterSelf().FirstOrDefault() is { } value)
            {
                value.ReplaceWith(new XElement("string", channel));
            }
            else
            {
                dict.Add(new XElement("key", key), new XElement("string", channel));
            }

            Directory.CreateDirectory(preferences);

            using (var writer = XmlWriter.Create(path, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true }))
                document.Save(writer);

            App.Logger.Info($"Set '{key}' in '{path}' to '{channel ?? "<unset>"}'");
        }
        catch (Exception ex)
        {
            App.Logger.Warn($"Failed to pin the channel in '{path}': {ex.Message}");
        }
    }

    private static XDocument NewPlist() => new(
        new XDeclaration("1.0", "UTF-8", null),
        new XDocumentType("plist", "-//Apple//DTD PLIST 1.0//EN", "http://www.apple.com/DTDs/PropertyList-1.0.dtd", null),
        new XElement("plist", new XAttribute("version", "1.0"), new XElement("dict")));
}
