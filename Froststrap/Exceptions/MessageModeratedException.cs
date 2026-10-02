namespace Froststrap.Exceptions
{
    internal class MessageModeratedException : Exception
    {
        public MessageModeratedException()
            : base("Message was moderated by the platform.") { }

        public MessageModeratedException(string message)
            : base(message) { }

        public MessageModeratedException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}