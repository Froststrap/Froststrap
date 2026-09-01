using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace Froststrap.UI.ViewModels.Onboarding
{
    internal class FaqItem : NotifyPropertyChangedViewModel
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

    internal class Page6ViewModel : NotifyPropertyChangedViewModel
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
                Question = Strings.Menu_Onboarding_Page6_Q1,
                Answer = Strings.Menu_Onboarding_Page6_A1
            });

            FaqItems.Add(new FaqItem
            {
                Question = Strings.Menu_Onboarding_Page6_Q2,
                Answer = Strings.Menu_Onboarding_Page6_A2
            });

            FaqItems.Add(new FaqItem
            {
                Question = Strings.Menu_Onboarding_Page6_Q3,
                Answer = Strings.Menu_Onboarding_Page6_A3
            });

            FaqItems.Add(new FaqItem
            {
                Question = Strings.Menu_Onboarding_Page6_Q4,
                Answer = Strings.Menu_Onboarding_Page6_A4
            });

            FaqItems.Add(new FaqItem
            {
                Question = Strings.Menu_Onboarding_Page6_Q5,
                Answer = Strings.Menu_Onboarding_Page6_A5
            });

            FaqItems.Add(new FaqItem
            {
                Question = Strings.Menu_Onboarding_Page6_Q6,
                Answer = Strings.Menu_Onboarding_Page6_A6
            });
        }
    }
}
