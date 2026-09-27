using System;

namespace Froststrap.Exceptions
{
    internal class SettingRejectedException : Exception
    {
        public string? Reason { get; }

        public SettingRejectedException()
        {
        }

        public SettingRejectedException(string? message)
            : base(message)
        {
        }

        public SettingRejectedException(string? message, Exception? innerException)
            : base(message, innerException)
        {
        }

        public SettingRejectedException(string setting, string value, int status, string? reason)
            : base($"{status} setting {setting} to {value}: {reason ?? "no reason given"}")
        {
            Reason = reason;
        }
    }
}