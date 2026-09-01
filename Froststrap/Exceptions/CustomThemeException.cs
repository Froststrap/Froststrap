namespace Froststrap.Exceptions
{
    internal class CustomThemeException : Exception
    {
        /// <summary>
        /// The exception message in English (for logging)
        /// </summary>
        public string EnglishMessage { get; } = null!;

        public CustomThemeException() : base() { }

        public CustomThemeException(string message) : base(message) { }

        public CustomThemeException(string message, Exception innerException) : base(message, innerException) { }

        public CustomThemeException(string translationString, params object?[] args)
            : base(string.Format(CultureInfo.InvariantCulture, Strings.ResourceManager.GetString(translationString, CultureInfo.CurrentCulture) ?? translationString, args))
        {
            EnglishMessage = string.Format(CultureInfo.InvariantCulture, Strings.ResourceManager.GetString(translationString, CultureInfo.InvariantCulture) ?? translationString, args);
        }

        public CustomThemeException(Exception innerException, string translationString)
            : base(Strings.ResourceManager.GetString(translationString, CultureInfo.CurrentCulture) ?? translationString, innerException)
        {
            EnglishMessage = Strings.ResourceManager.GetString(translationString, CultureInfo.InvariantCulture) ?? translationString;
        }

        public CustomThemeException(Exception innerException, string translationString, params object?[] args)
            : base(string.Format(CultureInfo.InvariantCulture, Strings.ResourceManager.GetString(translationString, CultureInfo.CurrentCulture) ?? translationString, args), innerException)
        {
            EnglishMessage = string.Format(CultureInfo.InvariantCulture, Strings.ResourceManager.GetString(translationString, CultureInfo.InvariantCulture) ?? translationString, args);
        }

        public override string ToString()
        {
            StringBuilder sb = new(GetType().ToString());

            if (!string.IsNullOrEmpty(Message))
                sb.Append(CultureInfo.InvariantCulture, $": {Message}");

            if (!string.IsNullOrEmpty(EnglishMessage) && Message != EnglishMessage)
                sb.Append(CultureInfo.InvariantCulture, $" ({EnglishMessage})");

            if (InnerException != null)
                sb.AppendFormat(CultureInfo.InvariantCulture, "\r\n ---> {0}\r\n   ", InnerException);

            if (StackTrace != null)
                sb.AppendFormat(CultureInfo.InvariantCulture, "\r\n{0}", StackTrace);

            return sb.ToString();
        }
    }
}