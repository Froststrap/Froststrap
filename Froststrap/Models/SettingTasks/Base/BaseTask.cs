namespace Froststrap.Models.SettingTasks.Base
{
    public abstract class BaseTask
    {
        public string Name { get; private set; }

        public abstract bool Changed { get; }

        public BaseTask(string prefix, string name) : this($"{prefix}.{name}") { }
        
        public BaseTask(string name) => Name = name;

        public override string ToString() => Name;

        public abstract void Execute();
    }
}
