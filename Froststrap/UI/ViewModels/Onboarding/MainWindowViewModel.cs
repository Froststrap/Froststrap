using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace Froststrap.UI.ViewModels.Onboarding
{
    public class MainWindowViewModel : NotifyPropertyChangedViewModel
    {
        public string NextButtonText { get; private set; } = Strings.Common_Next;

        public int ButtonWidth { get; } = Locale.CurrentCulture.Name.StartsWith("bg") ? 112 : 96;

        public ICommand BackPageCommand => new RelayCommand(BackPage);
        
        public ICommand NextPageCommand => new RelayCommand(NextPage);

        public event EventHandler<string>? PageRequest;

        public void SetNextButtonText(string text)
        {
            NextButtonText = text;
            OnPropertyChanged(nameof(NextButtonText));
        }

        private void BackPage() => PageRequest?.Invoke(this, "back");

        private void NextPage() => PageRequest?.Invoke(this, "next");
    }
}