using Froststrap.UI.ViewModels;

namespace Froststrap.Models
{
    public class CustomIntegration : NotifyPropertyChangedViewModel
    {
        private string _name = "";
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        private string _location = "";
        public string Location
        {
            get => _location;
            set { _location = value; OnPropertyChanged(nameof(Location)); }
        }

        private bool _specifyGame = false;
        public bool SpecifyGame
        {
            get => _specifyGame;
            set
            {
                _specifyGame = value;
                OnPropertyChanged(nameof(SpecifyGame));
            }
        }
        public string LaunchArgs { get; set; } = "";
        public string GameID { get; set; } = "";
        public bool AutoCloseOnGame { get; set; } = true;
        public int Delay { get; set; } = 0;
        public bool PreLaunch { get; set; } = false;
        public bool AutoClose { get; set; } = true;
    }
}
