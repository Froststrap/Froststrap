using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace Froststrap.UI.ViewModels.Onboarding
{
    public class FaqItem : NotifyPropertyChangedViewModel
    {
        private bool _isExpanded;

        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                OnPropertyChanged(nameof(IsExpanded));
            }
        }

        public ICommand ToggleCommand { get; }

        public FaqItem()
        {
            ToggleCommand = new RelayCommand(ToggleExpanded);
        }

        private void ToggleExpanded()
        {
            IsExpanded = !IsExpanded;
        }
    }

    public class Page6ViewModel : NotifyPropertyChangedViewModel
    {
        public ObservableCollection<FaqItem> FaqItems { get; } = [];

        public Page6ViewModel()
        {
            LoadFaqData();
        }

        private void LoadFaqData()
        {
            FaqItems.Add(new FaqItem
            {
                Question = "Q. Why isn't there multi instancing?",
                Answer = "Long story short: Roblox considers multi instancing exploiting and has been actively trying to patch it for a while now. Froststrap will no longer offer the feature nor support its use."
            });

            FaqItems.Add(new FaqItem
            {
                Question = "Q. Why is there so few FastFlags?",
                Answer = "Roblox has implemented a fflag allowlist, preventing the use of majority of fflags. This was done to prevent people from using exploitable fflags, and to add bogus fflags that could cause bugs. To see the whitelist, go [here](https://devforum.roblox.com/t/allowlist-for-local-client-configuration-via-fast-flags/3966569)"
            });

            FaqItems.Add(new FaqItem
            {
                Question = "Q. Help! I enabled some fflags and now my lighting doesn't work!",
                Answer = "You enabled the Pause Voxelizer fflag (DFFlagDebugPauseVoxelizer), disable it to fix the issue."
            });

            FaqItems.Add(new FaqItem
            {
                Question = "Q. Whenever I launch Roblox via. Froststrap, I constantly get signed out of Roblox and am forced to resignin everytime.",
                Answer = "Delete RobloxCookies.dat inside %localappdata%\\Roblox\\LocalStorage, which should fix the issue majority of the time. We do not know what causes this issue."
            });

            FaqItems.Add(new FaqItem
            {
                Question = "Q. How can I get blurry/no textures?",
                Answer = "You don't. You may be able to achieve this with alternative software but we do not guarantee safety nor practicality."
            });

            FaqItems.Add(new FaqItem
            {
                Question = "Q. How do I reduce ping/ increase fps with fflags?",
                Answer = "You don't, again. You may try to use some fflags to achieve higher fps on higher render distances, however lowering ping via fflags is not possible."
            });
        }
    }
}
