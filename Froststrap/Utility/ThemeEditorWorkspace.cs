// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Xml.Linq;

namespace Froststrap.Utility
{
    internal static class ThemeEditorWorkspace
    {
        private const string VsCodeDirectoryName = ".vscode";
        private const string SchemaFileName = "theme.xsd";
        private const string SchemaNamespace = "http://www.w3.org/2001/XMLSchema";

        private const string ExtensionsJson = """
            {
              "recommendations": [
                "redhat.vscode-xml"
              ]
            }
            """;

        private const string SettingsJson = """
            {
              "xml.fileAssociations": [
                {
                  "pattern": "Theme.xml",
                  "systemId": ".vscode/theme.xsd"
                }
              ],
              "xml.validation.enabled": true
            }
            """;

        private static readonly string[] GeneratedVsCodeFiles = [SchemaFileName, "settings.json", "extensions.json"];

        private static string? _cachedSchema;

        public static void RemoveEditorConfiguration(string themeDirectory)
        {
            foreach (string entry in Directory.EnumerateFileSystemEntries(themeDirectory, ".*", SearchOption.AllDirectories).ToList())
            {
                try
                {
                    if (Directory.Exists(entry))
                        Directory.Delete(entry, true);
                    else if (File.Exists(entry))
                        File.Delete(entry);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    App.Logger.Warn(ex, $"Failed to remove editor configuration '{entry}'");
                }
            }
        }

        public static void EnsureVisualStudioCode(string themeDirectory)
        {
            try
            {
                string directory = Path.Combine(themeDirectory, VsCodeDirectoryName);
                Directory.CreateDirectory(directory);

                foreach (string file in Directory.EnumerateFileSystemEntries(directory))
                {
                    if (GeneratedVsCodeFiles.Contains(Path.GetFileName(file), StringComparer.OrdinalIgnoreCase))
                        continue;

                    if (Directory.Exists(file))
                        Directory.Delete(file, true);
                    else
                        File.Delete(file);
                }

                WriteIfChanged(Path.Combine(directory, SchemaFileName), GetSchema());
                WriteIfChanged(Path.Combine(directory, "settings.json"), SettingsJson);
                WriteIfChanged(Path.Combine(directory, "extensions.json"), ExtensionsJson);
            }
            catch (Exception ex)
            {
                App.Logger.Error(ex, "Failed to prepare the theme editor workspace");
            }
        }

        private static void WriteIfChanged(string path, string contents)
        {
            if (File.Exists(path) && File.ReadAllText(path) == contents)
                return;

            File.WriteAllText(path, contents);
        }

        private static string GetSchema()
        {
            if (_cachedSchema != null)
                return _cachedSchema;

            using Stream stream = Resource.GetStream("CustomBootstrapperSchema.json");
            using JsonDocument document = JsonDocument.Parse(stream);

            _cachedSchema = BuildSchema(document.RootElement).ToString();
            return _cachedSchema;
        }

        private static XDocument BuildSchema(JsonElement root)
        {
            XNamespace xs = SchemaNamespace;

            Dictionary<string, TypeDefinition> types = [];
            foreach (JsonProperty property in root.GetProperty("Types").EnumerateObject())
            {
                List<string>? values = null;
                if (property.Value.TryGetProperty("Values", out JsonElement valuesElement))
                    values = [.. valuesElement.EnumerateArray().Select(x => x.GetString() ?? string.Empty)];

                bool canHaveElement = property.Value.TryGetProperty("CanHaveElement", out JsonElement canHaveElementElement)
                    && canHaveElementElement.GetBoolean();

                types[property.Name] = new TypeDefinition(canHaveElement, values);
            }

            Dictionary<string, ElementDefinition> elements = [];
            foreach (JsonProperty property in root.GetProperty("Elements").EnumerateObject())
            {
                Dictionary<string, string> attributes = [];
                if (property.Value.TryGetProperty("Attributes", out JsonElement attributesElement))
                {
                    foreach (JsonProperty attribute in attributesElement.EnumerateObject())
                        attributes[attribute.Name] = attribute.Value.GetString() ?? "string";
                }

                string? superClass = property.Value.TryGetProperty("SuperClass", out JsonElement superElement)
                    ? superElement.GetString()
                    : null;

                bool isCreatable = property.Value.TryGetProperty("IsCreatable", out JsonElement creatableElement)
                    && creatableElement.GetBoolean();

                elements[property.Name] = new ElementDefinition(superClass, isCreatable, attributes);
            }

            string[] creatableElements = [.. elements.Where(e => e.Value.IsCreatable).Select(e => e.Key)];

            XElement schema = new(xs + "schema", new XAttribute(XNamespace.Xmlns + "xs", SchemaNamespace));

            foreach ((string name, TypeDefinition definition) in types.Where(t => t.Value.Values is { Count: > 0 }))
            {
                schema.Add(new XElement(xs + "simpleType",
                    new XAttribute("name", $"Type_{name}"),
                    new XElement(xs + "restriction",
                        new XAttribute("base", "xs:string"),
                        definition.Values!.Select(value => new XElement(xs + "enumeration", new XAttribute("value", value))))));
            }

            schema.Add(new XElement(xs + "complexType",
                new XAttribute("name", "PropertyElementType"),
                new XAttribute("mixed", "true"),
                new XElement(xs + "sequence",
                    new XElement(xs + "choice",
                        new XAttribute("minOccurs", "0"),
                        new XAttribute("maxOccurs", "unbounded"),
                        creatableElements.Select(name => new XElement(xs + "element",
                            new XAttribute("ref", name),
                            new XAttribute("minOccurs", "0")))))));

            Dictionary<string, Dictionary<string, string>> attributeCache = [];

            Dictionary<string, string> GetAttributes(string elementName)
            {
                if (attributeCache.TryGetValue(elementName, out Dictionary<string, string>? cached))
                    return cached;

                ElementDefinition definition = elements[elementName];
                Dictionary<string, string> result = [];

                if (definition.SuperClass != null)
                {
                    foreach ((string name, string type) in GetAttributes(definition.SuperClass))
                        result[name] = type;
                }

                foreach ((string name, string type) in definition.Attributes)
                    result[name] = type;

                attributeCache[elementName] = result;
                return result;
            }

            foreach (string elementName in creatableElements)
            {
                List<string> names = [elementName];

                if (elementName == "BloxstrapCustomBootstrapper")
                    names.Add("Window");
                else if (elementName == "TextBlock")
                    names.Add("Label");

                Dictionary<string, string> attributes = GetAttributes(elementName);
                List<XElement> propertyElementReferences = [];

                foreach (string name in names)
                {
                    foreach ((string attributeName, string attributeType) in attributes)
                    {
                        if (!types.TryGetValue(attributeType, out TypeDefinition? type) || !type.CanHaveElement)
                            continue;

                        string propertyElementName = $"{name}.{attributeName}";

                        schema.Add(new XElement(xs + "element",
                            new XAttribute("name", propertyElementName),
                            new XAttribute("type", "PropertyElementType")));

                        propertyElementReferences.Add(new XElement(xs + "element",
                            new XAttribute("ref", propertyElementName),
                            new XAttribute("minOccurs", "0")));
                    }
                }

                XElement choice = new(xs + "choice",
                    new XAttribute("minOccurs", "0"),
                    new XAttribute("maxOccurs", "unbounded"));

                foreach (string child in creatableElements)
                {
                    choice.Add(new XElement(xs + "element",
                        new XAttribute("ref", child),
                        new XAttribute("minOccurs", "0")));
                }

                foreach (XElement reference in propertyElementReferences)
                    choice.Add(reference);

                XElement complexType = new(xs + "complexType", new XAttribute("name", $"{elementName}Type"));
                complexType.Add(new XElement(xs + "sequence", choice));

                foreach ((string attributeName, string attributeType) in attributes)
                {
                    complexType.Add(new XElement(xs + "attribute",
                        new XAttribute("name", attributeName),
                        new XAttribute("type", MapType(attributeType, types)),
                        new XAttribute("use", "optional")));
                }

                schema.Add(complexType);

                foreach (string name in names)
                {
                    schema.Add(new XElement(xs + "element",
                        new XAttribute("name", name),
                        new XAttribute("type", $"{elementName}Type")));
                }
            }

            return new XDocument(schema);
        }

        private static string MapType(string typeName, Dictionary<string, TypeDefinition> types)
        {
            if (types.TryGetValue(typeName, out TypeDefinition? definition) && definition.Values is { Count: > 0 })
                return $"Type_{typeName}";

            return typeName switch
            {
                "int" => "xs:int",
                "double" => "xs:double",
                _ => "xs:string"
            };
        }

        private sealed record TypeDefinition(bool CanHaveElement, List<string>? Values);

        private sealed record ElementDefinition(string? SuperClass, bool IsCreatable, Dictionary<string, string> Attributes);
    }
}
