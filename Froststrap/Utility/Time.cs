namespace Froststrap.Utility
{
    internal static class Time
    {
        public static string FormatTimeSpan(TimeSpan timeSpan)
        {
            static string FormatTuple((int Value, string Name) t) =>
                $"{t.Value} {t.Name}{(t.Value == 1 ? string.Empty : "s")}";

            var components = new List<(int Value, string Name)>
            {
                ((int)timeSpan.TotalDays, "day"),
                (timeSpan.Hours, "hour"),
                (timeSpan.Minutes, "minute")
            };

            components.RemoveAll(i => i.Value == 0);

            string extra = "";

            if (components.Count > 1)
            {
                var finalComponent = components[^1];
                components.RemoveAt(components.Count - 1);
                extra = $" and {FormatTuple(finalComponent)}";
            }

            return $"{string.Join(", ", components.Select(FormatTuple))}{extra}";
        }
    }
}