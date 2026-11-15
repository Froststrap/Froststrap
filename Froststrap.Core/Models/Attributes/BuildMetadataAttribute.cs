// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Models.Attributes
{
    [AttributeUsage(AttributeTargets.Assembly)]
    internal sealed class BuildMetadataAttribute(string timestamp, string commitHash, string commitRef) : Attribute
    {
        public string Timestamp { get; } = timestamp;
        public string CommitHash { get; } = commitHash;
        public string CommitRef { get; } = commitRef;

        public DateTime LocalTimestamp =>
            DateTime.Parse(Timestamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToLocalTime();
    }
}
