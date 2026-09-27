using Avalonia.Threading;

namespace Froststrap.UI.ViewModels.Overlay.Controls
{
    internal class NotesViewModel : NotifyPropertyChangedViewModel
    {
        private static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(2);

        private readonly DispatcherTimer _saveTimer;

        private bool _loaded;

        public NotesViewModel()
        {
            _saveTimer = new DispatcherTimer { Interval = SaveDelay };
            _saveTimer.Tick += (_, _) => Save();

            Load();
        }

        private string _content = String.Empty;

        public string Content
        {
            get => _content;
            set
            {
                if (_content == value)
                    return;

                _content = value;

                OnPropertyChanged(nameof(Content));

                if (!_loaded)
                    return;

                Status = Strings.Menu_Overlay_Notes_Unsaved;

                _saveTimer.Stop();
                _saveTimer.Start();
            }
        }

        private string _status = String.Empty;

        public string Status
        {
            get => _status;
            private set
            {
                if (_status == value)
                    return;

                _status = value;

                OnPropertyChanged(nameof(Status));
            }
        }

        private void Load()
        {
            try
            {
                if (!App.OverlayNotes.Loaded)
                    App.OverlayNotes.Load(false);

                Content = App.OverlayNotes.Prop.Content;

                if (App.OverlayNotes.Prop.UpdatedAt is DateTime updated)
                    Status = String.Format(
                        Locale.CurrentCulture,
                        Strings.Menu_Overlay_Notes_SavedAt,
                        updated.ToLocalTime().ToString("t", Locale.CurrentCulture));
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to load notes");
                App.Logger.Error(ex);
            }
            finally
            {
                _loaded = true;
            }
        }

        public void Save()
        {
            _saveTimer.Stop();

            if (!_loaded || App.OverlayNotes.Prop.Content == _content)
                return;

            try
            {
                DateTime now = DateTime.Now;

                App.OverlayNotes.Prop.Content = _content;
                App.OverlayNotes.Prop.UpdatedAt = now;
                App.OverlayNotes.Save();

                Status = String.Format(
                    Locale.CurrentCulture,
                    Strings.Menu_Overlay_Notes_SavedAt,
                    now.ToString("t", Locale.CurrentCulture));
            }
            catch (Exception ex)
            {
                App.Logger.Error("Failed to save notes");
                App.Logger.Error(ex);

                Status = Strings.Menu_Overlay_Notes_SaveFailed;
            }
        }
    }
}