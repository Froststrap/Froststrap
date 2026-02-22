using System.Xml.Linq;
using System.Xml.XPath;

namespace Bloxstrap
{
    public class GBSEditor
    {
        private static readonly Regex PathResolveRegex = new(@"\{(.+?)\}", RegexOptions.Compiled);
        private static readonly Regex SanitizeNameRegex = new(@"[@'\[\]]", RegexOptions.Compiled);

        public XDocument? Document { get; set; }
        public bool Loaded { get; private set; }
        public bool PreviousReadOnlyState { get; set; }
        public string FileLocation => Path.Combine(Paths.Roblox, "GlobalBasicSettings_13.xml");

        public readonly Dictionary<string, string> PresetPaths = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Rendering.FramerateCap", "{UserSettings}/int[@name='FramerateCap']" }
        };

        public readonly Dictionary<string, string> RootPaths = new(StringComparer.OrdinalIgnoreCase)
        {
            { "UserSettings", "//Item[@class='UserGameSettings']/Properties" }
        };

        public void SetValue(string xmlPath, string dataType, object? value)
        {
            if (!Loaded || Document == null) return;

            xmlPath = ResolvePath(xmlPath);
            XElement? element = Document.XPathSelectElement(xmlPath) ?? CreateElement(xmlPath, dataType);

            if (element == null) return;

            string stringValue = value?.ToString() ?? string.Empty;

            if (dataType.Equals("vector2", StringComparison.OrdinalIgnoreCase))
            {
                var parts = stringValue.Split(',');
                if (parts.Length == 2)
                {
                    element.ReplaceNodes(new XElement("X", parts[0]), new XElement("Y", parts[1]));
                }
                return;
            }

            if (dataType.Equals("bool", StringComparison.OrdinalIgnoreCase) && bool.TryParse(stringValue, out bool boolVal))
                element.Value = boolVal.ToString().ToLower();
            else
                element.Value = stringValue;
        }

        public string? GetValue(string xmlPath, string dataType)
        {
            if (!Loaded || Document == null) return null;

            xmlPath = ResolvePath(xmlPath);
            XElement? element = Document.XPathSelectElement(xmlPath);

            if (element == null) return null;

            if (dataType.Equals("vector2", StringComparison.OrdinalIgnoreCase))
            {
                var x = element.Element("X")?.Value ?? "0";
                var y = element.Element("Y")?.Value ?? "0";
                return $"{x},{y}";
            }

            return element.Value;
        }

        private XElement? CreateElement(string xmlPath, string dataType)
        {
            try
            {
                var segments = xmlPath.Split('/');
                string nameAttr = SanitizeNameRegex.Replace(segments.Last(), "");

                XElement newElement = dataType.ToLower() switch
                {
                    "vector2" => new XElement("Vector2", new XAttribute("name", nameAttr), new XElement("X", "0"), new XElement("Y", "0")),
                    "int" or "float" or "bool" or "token" => new XElement(dataType.ToLower(), new XAttribute("name", nameAttr)),
                    _ => new XElement("string", new XAttribute("name", nameAttr))
                };

                string parentPath = string.Join("/", segments.Take(segments.Length - 1));
                XElement? parent = Document?.XPathSelectElement(parentPath) ?? Document?.Root;

                parent?.Add(newElement);
                return newElement;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("GBSEditor::CreateElement", $"Failed to create element: {ex.Message}");
                return null;
            }
        }

        public void Load()
        {
            try
            {
                if (File.Exists(FileLocation))
                {
                    Document = XDocument.Load(FileLocation);
                    PreviousReadOnlyState = GetReadOnly();
                }
                else
                {
                    Document = new XDocument(new XElement("roblox"));
                }
                Loaded = true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("GBSEditor::Load", "Failed to load!");
                App.Logger.WriteException("GBSEditor::Load", ex);
                Document = new XDocument(new XElement("roblox"));
                Loaded = true;
            }
        }

        public virtual void Save()
        {
            if (!Loaded || Document == null) return;

            try
            {
                string? directory = Path.GetDirectoryName(FileLocation);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                SetReadOnly(false, true);
                Document.Save(FileLocation);
                SetReadOnly(PreviousReadOnlyState);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("GBSEditor::Save", ex);
            }
        }

        private string ResolvePath(string rawPath) => PathResolveRegex.Replace(rawPath, match => RootPaths.GetValueOrDefault(match.Groups[1].Value, match.Value));

        public void SetReadOnly(bool readOnly, bool preserveState = false)
        {
            if (!File.Exists(FileLocation)) return;
            try
            {
                var attr = File.GetAttributes(FileLocation);
                if (readOnly) attr |= FileAttributes.ReadOnly;
                else attr &= ~FileAttributes.ReadOnly;

                File.SetAttributes(FileLocation, attr);
                if (!preserveState) PreviousReadOnlyState = readOnly;
            }
            catch (Exception ex) { App.Logger.WriteException("GBSEditor::SetReadOnly", ex); }
        }

        public bool GetReadOnly() => File.Exists(FileLocation) && File.GetAttributes(FileLocation).HasFlag(FileAttributes.ReadOnly);

        public bool ExportSettings(string exportPath)
        {
            try
            {
                if (!File.Exists(FileLocation)) return false;
                string? dir = Path.GetDirectoryName(exportPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.Copy(FileLocation, exportPath, true);
                return true;
            }
            catch { return false; }
        }

        public bool ImportSettings(string importPath)
        {
            try
            {
                if (!File.Exists(importPath)) return false;
                SetReadOnly(false, true);
                File.Copy(importPath, FileLocation, true);
                Load();
                return true;
            }
            catch { return false; }
        }

        public void SetPresets(string prefix, object? value)
        {
            foreach (var pair in PresetPaths.Where(x => x.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                SetValue(pair.Value, "string", value);
        }

        public string? GetPresets(string prefix) => PresetPaths.TryGetValue(prefix, out var path) ? GetValue(path, "string") : null;
    }
}