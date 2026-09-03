using Avalonia.Media.Imaging;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Froststrap.UI.Converters;
using Froststrap.UI.ViewModels;

namespace Froststrap.Models
{
    internal class ServerEntry : NotifyPropertyChangedViewModel
    {
        private string _extraPlayersText = "";
        private bool _hasExtraPlayers;
        private ObservableCollection<Bitmap> _playerAvatarThumbnails = [];
        public int Number { get; set; }
        public string ServerId { get; set; } = null!;
        public string Players { get; set; } = null!;
        public int PlayingCount { get; set; }
        public string Region { get; set; } = null!;
        public int? DataCenterId { get; set; }
        public string Uptime { get; set; } = "Loading...";
        public ICommand? JoinCommand { get; set; }
        public List<string> PlayerTokens { get; set; } = [];

        public ObservableCollection<Bitmap> PlayerAvatarThumbnails
        {
            get => _playerAvatarThumbnails;
            set => SetProperty(ref _playerAvatarThumbnails, value);
        }

        public string ExtraPlayersText
        {
            get => _extraPlayersText;
            set => SetProperty(ref _extraPlayersText, value);
        }

        public bool HasExtraPlayers
        {
            get => _hasExtraPlayers;
            set => SetProperty(ref _hasExtraPlayers, value);
        }
    }
}