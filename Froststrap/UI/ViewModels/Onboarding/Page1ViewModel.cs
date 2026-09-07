// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.UI.ViewModels.Onboarding
{
    internal class Page1ViewModel : NotifyPropertyChangedViewModel
    {
        public Page1ViewModel() { }

        public static List<string> Languages => Locale.GetLanguages();

        private string _selectedLanguage = Locale.SupportedLocales[App.Settings.Prop.Locale];
        public string SelectedLanguage
        {
            get => _selectedLanguage;
            set
            {
                if (SetProperty(ref _selectedLanguage, value))
                {
                    SetLocale();
                }
            }
        }

        private void SetLocale()
        {
            string identifier = Locale.GetIdentifierFromName(SelectedLanguage);
            Locale.Set(identifier);
            App.Settings.Prop.Locale = identifier;
        }
    }
}
