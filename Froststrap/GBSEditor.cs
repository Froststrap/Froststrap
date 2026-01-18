using System.Xml.Linq;
using System.Xml.XPath;

namespace Froststrap
{
    public class GBSEditor
    { 
        // Fishstrap GBSEditor converted into a remote config version
        public XDocument? Document { get; set; } = null!;

        public Dictionary<string, string> PresetPaths = new()
        {
            { "Rendering.FramerateCap", "{UserSettings}/int[@name='FramerateCap']" }
        };

        public Dictionary<string, string> RootPaths = new()
        {
            { "UserSettings", "//Item[@class='UserGameSettings']/Properties" }
        };

        public bool Loaded { get; set; } = false;

        public string FileLocation => Path.Combine(Paths.Roblox, "GlobalBasicSettings_13.xml");

        public void SetValue(string xmlPath, string dataType, object? value)
        {
            if (!Loaded) return;

            xmlPath = ResolvePath(xmlPath);
            XElement? element = Document?.XPathSelectElement(xmlPath);

            if (element is null)
            {
                element = CreateElement(xmlPath, dataType);
                if (element is null) return;
            }

            string stringValue = value?.ToString() ?? "";

            switch (dataType.ToLower())
            {
                case "vector2":
                    var parts = stringValue.Split(',');
                    if (parts.Length == 2)
                    {
                        element.Elements("X").Remove();
                        element.Elements("Y").Remove();
                        element.Add(new XElement("X", parts[0]));
                        element.Add(new XElement("Y", parts[1]));
                    }
                    break;
                case "int":
                    if (int.TryParse(stringValue, out int intValue))
                        element.Value = intValue.ToString();
                    break;
                case "float":
                    if (float.TryParse(stringValue, out float floatValue))
                        element.Value = floatValue.ToString();
                    break;
                case "bool":
                    if (bool.TryParse(stringValue, out bool boolValue))
                        element.Value = boolValue.ToString().ToLower();
                    break;
                default:
                    element.Value = stringValue;
                    break;
            }
        }

        public string? GetValue(string xmlPath, string dataType)
        {
            if (!Loaded) return null;

            xmlPath = ResolvePath(xmlPath);
            var element = Document?.XPathSelectElement(xmlPath);

            if (element is null)
                return null;

            if (dataType.ToLower() == "vector2")
            {
                var xElement = element.XPathSelectElement("X");
                var yElement = element.XPathSelectElement("Y");

                if (xElement != null && yElement != null)
                    return $"{xElement.Value},{yElement.Value}";

                return "0,0";
            }

            return element.Value;
        }

        private XElement? CreateElement(string xmlPath, string dataType)
        {
            try
            {
                var elements = xmlPath.Split('/');
                var lastElement = elements.Last();

                XElement newElement;

                if (dataType.ToLower() == "vector2")
                {
                    newElement = new XElement("Vector2",
                        new XAttribute("name", lastElement.TrimStart('@').Replace("'", "").Replace("[", "").Replace("]", "")),
                        new XElement("X", "0"),
                        new XElement("Y", "0")
                    );
                }
                else
                {
                    newElement = dataType.ToLower() switch
                    {
                        "int" => new XElement("int", new XAttribute("name", lastElement.TrimStart('@').Replace("'", "").Replace("[", "").Replace("]", ""))),
                        "float" => new XElement("float", new XAttribute("name", lastElement.TrimStart('@').Replace("'", "").Replace("[", "").Replace("]", ""))),
                        "bool" => new XElement("bool", new XAttribute("name", lastElement.TrimStart('@').Replace("'", "").Replace("[", "").Replace("]", ""))),
                        "token" => new XElement("token", new XAttribute("name", lastElement.TrimStart('@').Replace("'", "").Replace("[", "").Replace("]", ""))),
                        _ => new XElement("string", new XAttribute("name", lastElement.TrimStart('@').Replace("'", "").Replace("[", "").Replace("]", "")))
                    };
                }

                var parentPath = string.Join("/", elements.Take(elements.Length - 1));
                var parent = Document?.XPathSelectElement(parentPath) ?? Document?.Root;

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
            if (!File.Exists(FileLocation))
            {
                Document = new XDocument(new XElement("roblox"));
                Loaded = true;
                return;
            }

            try
            {
                Document = XDocument.Load(FileLocation);
                Loaded = true;
                previousReadOnlyState = GetReadOnly();
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
            if (!Loaded) return;

            try
            {
                var directory = Path.GetDirectoryName(FileLocation);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                SetReadOnly(false, true);
                Document?.Save(FileLocation);
                SetReadOnly(previousReadOnlyState);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("GBSEditor::Save", "Failed to save");
                App.Logger.WriteException("GBSEditor::Save", ex);
            }
        }

        private string ResolvePath(string rawPath)
        {
            return Regex.Replace(rawPath, @"\{(.+?)\}", match =>
            {
                string key = match.Groups[1].Value;
                return RootPaths.TryGetValue(key, out var value) ? value : match.Value;
            });
        }

        public bool previousReadOnlyState;

        public void SetReadOnly(bool readOnly, bool preserveState = false)
        {
            if (!File.Exists(FileLocation)) return;

            try
            {
                FileAttributes attributes = File.GetAttributes(FileLocation);

                if (readOnly)
                    attributes |= FileAttributes.ReadOnly;
                else
                    attributes &= ~FileAttributes.ReadOnly;

                File.SetAttributes(FileLocation, attributes);

                if (!preserveState)
                    previousReadOnlyState = readOnly;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("GBSEditor::SetReadOnly", $"Failed to set read-only on {FileLocation}");
                App.Logger.WriteException("GBSEditor::SetReadOnly", ex);
            }
        }

        public bool GetReadOnly()
        {
            if (!File.Exists(FileLocation)) return false;
            return File.GetAttributes(FileLocation).HasFlag(FileAttributes.ReadOnly);
        }

        public bool ExportSettings(string exportPath)
        {
            if (!File.Exists(FileLocation))
                return false;

            try
            {
                var directory = Path.GetDirectoryName(exportPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.Copy(FileLocation, exportPath, true);
                return true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("GBSEditor::ExportSettings", $"Failed to export settings: {ex.Message}");
                return false;
            }
        }

        public bool ImportSettings(string importPath)
        {
            if (!File.Exists(importPath))
                return false;

            try
            {
                var directory = Path.GetDirectoryName(FileLocation);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                SetReadOnly(false, true);

                File.Copy(importPath, FileLocation, true);

                Load();

                return true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine("GBSEditor::ImportSettings", $"Failed to import settings: {ex.Message}");
                return false;
            }
        }

        public void SetPresets(string prefix, object? value)
        {
            foreach (var pair in PresetPaths.Where(x => x.Key.StartsWith(prefix)))
                SetValues(pair.Value, value);
        }

        public string? GetPresets(string prefix)
        {
            if (!PresetPaths.ContainsKey(prefix))
                return null;

            return GetValues(PresetPaths[prefix]);
        }

        public void SetValues(string path, object? value)
        {
            path = ResolvePath(path);

            XElement? element = Document?.XPathSelectElement(path);
            if (element is null)
                return;

            element.Value = value?.ToString()!;
        }

        public string? GetValues(string path)
        {
            path = ResolvePath(path);

            return Document?.XPathSelectElement(path)?.Value;
        }
    }
}