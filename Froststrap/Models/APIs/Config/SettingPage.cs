using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Froststrap.Models.APIs.Config
{
    public class SettingsPageConfig
    {
        [JsonPropertyName("sections")]
        public List<SettingsSection> Sections { get; set; } = new();
    }

    public class SettingsSection
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("controls")]
        public List<SettingsControl> Controls { get; set; } = new();
    }

    public class SettingsControl : INotifyPropertyChanged
    {
        [JsonPropertyName("type")]
        public ControlType Type { get; set; } = ControlType.TextBox;

        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        private string _value = "";

        [JsonPropertyName("value")]
        public string Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TypedValue));
                    OnPropertyChanged(nameof(VectorX));
                    OnPropertyChanged(nameof(VectorY));
                }
            }
        }

        [JsonIgnore]
        public object TypedValue
        {
            get
            {
                // Only convert when we're sure about the target type
                return Type switch
                {
                    ControlType.Slider => GetSliderValue(),
                    ControlType.ToggleSwitch => GetToggleSwitchValue(),
                    ControlType.Vector2 => Value, // Let VectorX/VectorY handle this
                    _ => Value
                };
            }
            set
            {
                // Only convert back when we know the source type matches our control type
                switch (Type)
                {
                    case ControlType.Slider when value is double doubleValue:
                        Value = doubleValue.ToString();
                        break;
                    case ControlType.ToggleSwitch when value is bool boolValue:
                        Value = boolValue.ToString().ToLower();
                        break;
                    default:
                        // For other types, don't try to convert - this prevents the errors
                        break;
                }
            }
        }

        private double GetSliderValue()
        {
            if (double.TryParse(Value, out double result) && result >= MinValue && result <= MaxValue)
                return result;
            return MinValue;
        }

        private bool GetToggleSwitchValue()
        {
            return Value.ToLower() == "true" || Value == "1";
        }

        [JsonPropertyName("minValue")]
        public double MinValue { get; set; } = 0;

        [JsonPropertyName("maxValue")]
        public double MaxValue { get; set; } = 100;

        [JsonPropertyName("step")]
        public double Step { get; set; } = 1;

        [JsonIgnore]
        public string VectorX
        {
            get
            {
                if (Type == ControlType.Vector2 && Vector2.TryParse(Value, out Vector2 vector))
                    return vector.X.ToString();
                return "0";
            }
            set
            {
                if (Type == ControlType.Vector2 && float.TryParse(value, out float x))
                {
                    Vector2 vector = Vector2.TryParse(Value, out Vector2 current) ? current : new Vector2();
                    vector.X = x;
                    Value = vector.ToString();
                }
            }
        }

        [JsonIgnore]
        public string VectorY
        {
            get
            {
                if (Type == ControlType.Vector2 && Vector2.TryParse(Value, out Vector2 vector))
                    return vector.Y.ToString();
                return "0";
            }
            set
            {
                if (Type == ControlType.Vector2 && float.TryParse(value, out float y))
                {
                    Vector2 vector = Vector2.TryParse(Value, out Vector2 current) ? current : new Vector2();
                    vector.Y = y;
                    Value = vector.ToString();
                }
            }
        }

        [JsonPropertyName("options")]
        public List<ControlOption> Options { get; set; } = new();

        [JsonPropertyName("gbsConfig")]
        public GBSConfig GBSConfig { get; set; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class GBSConfig
    {
        [JsonPropertyName("xmlPath")]
        public string XmlPath { get; set; } = "";

        [JsonPropertyName("xmlPaths")]
        public List<string> XmlPaths { get; set; } = new();

        [JsonPropertyName("dataType")]
        public string DataType { get; set; } = "string";
    }

    public class ControlOption
    {
        [JsonPropertyName("value")]
        public string Value { get; set; } = "";

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = "";
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ControlType
    {
        TextBox,
        Slider,
        ToggleSwitch,
        ComboBox,
        Vector2
    }

    public class Vector2
    {
        [JsonPropertyName("x")]
        public float X { get; set; }

        [JsonPropertyName("y")]
        public float Y { get; set; }

        public Vector2() { }

        public Vector2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public override string ToString()
        {
            return $"{X},{Y}";
        }

        public static bool TryParse(string value, out Vector2 result)
        {
            result = new Vector2();

            if (string.IsNullOrEmpty(value))
                return false;

            var parts = value.Split(',');
            if (parts.Length != 2)
                return false;

            if (float.TryParse(parts[0], out float x) && float.TryParse(parts[1], out float y))
            {
                result = new Vector2(x, y);
                return true;
            }

            return false;
        }
    }
}