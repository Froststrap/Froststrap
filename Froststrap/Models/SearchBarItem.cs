using System;

namespace Froststrap.Models
{
    public class SearchBarItem
    {
        public string DisplayName { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public Action? NavigateAction { get; set; }
        public override string ToString() => DisplayName;
    }
}
