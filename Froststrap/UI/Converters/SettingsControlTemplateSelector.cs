using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Froststrap.Models.APIs.Config;

namespace Froststrap.UI.Converters
{
    public class SettingsControlTemplateSelector : IDataTemplate
    {
        public Control? Build(object? param)
        {
            if (param is SettingsControl control)
            {
                string resourceName = control.Type switch
                {
                    ControlType.TextBox => "TextBoxTemplate",
                    ControlType.Slider => "SliderTemplate",
                    ControlType.ToggleSwitch => "ToggleSwitchTemplate",
                    ControlType.ComboBox => "ComboBoxTemplate",
                    ControlType.Vector2 => "Vector2Template",
                    _ => "TextBoxTemplate"
                };

                return null;
            }

            return null;
        }

        public bool Match(object? data)
        {
            return data is SettingsControl;
        }
    }
}