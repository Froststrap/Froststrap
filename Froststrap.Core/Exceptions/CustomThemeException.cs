// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

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
            : base(string.Format(CultureInfo.InvariantCulture, Translations.Get(translationString), args))
        {
            EnglishMessage = string.Format(CultureInfo.InvariantCulture, Translations.GetEnglish(translationString), args);
        }

        public CustomThemeException(Exception innerException, string translationString)
            : base(Translations.Get(translationString), innerException)
        {
            EnglishMessage = Translations.GetEnglish(translationString);
        }

        public CustomThemeException(Exception innerException, string translationString, params object?[] args)
            : base(string.Format(CultureInfo.InvariantCulture, Translations.Get(translationString), args), innerException)
        {
            EnglishMessage = string.Format(CultureInfo.InvariantCulture, Translations.GetEnglish(translationString), args);
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