namespace Froststrap.Models.SettingTasks
{
    public class ShortcutTask : BoolBaseTask
    {
        private readonly string _shortcutPath;

        private readonly string _exeFlags;

        public ShortcutTask(string name, string lnkFolder, string lnkName, string exeFlags = "") : base("Shortcut", name)
        {
            _shortcutPath = Path.Combine(lnkFolder, lnkName);
            _exeFlags = exeFlags;
            
            OriginalState = File.Exists(Shortcut.ResolvePath(_shortcutPath));
        }

        public override void Execute()
        {
            if (NewState)
                Shortcut.Create(Paths.Application, _exeFlags, _shortcutPath);
            else
                Shortcut.Delete(_shortcutPath);

            OriginalState = NewState;
        }
    }
}