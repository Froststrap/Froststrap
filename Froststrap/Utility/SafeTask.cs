namespace Froststrap.Utility
{
    public enum ErrorSeverity
    {
        NonFatal,
        Degraded,
        Fatal
    }

    public static class SafeTask
    {
        public static void Run(
            Func<Task> action,
            ErrorSeverity severity = ErrorSeverity.NonFatal,
            string logIdent = "SafeTask")
        {
            Task.Run(action).ContinueWith(t =>
            {
                if (!t.IsFaulted || t.Exception is null) return;

                var ex = t.Exception.GetBaseException();
                App.Logger.WriteException(logIdent, ex);

                _ = App.FinalizeExceptionHandling(ex, severity, alreadyLogged: true);
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        public static void Run(
            Action action,
            ErrorSeverity severity = ErrorSeverity.NonFatal,
            string logIdent = "SafeTask")
            => Run(() => { action(); return Task.CompletedTask; }, severity, logIdent);
    }
}
