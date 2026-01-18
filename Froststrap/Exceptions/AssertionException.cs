namespace Froststrap.Exceptions
{
    internal class AssertionException : Exception
    {
        public AssertionException(string message)
            : base($"{message}\n\nThis is very likely just an off-chance error. Please report this first, and then start {App.ProjectName} again.")
        { 
        }
    }
}
