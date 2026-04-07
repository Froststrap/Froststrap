
namespace Froststrap.Models
{
    public class SearchBarItem
    {
        public string DisplayName { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public Action? NavigateAction { get; set; }
        public string? PageTag { get; set; }
        public string? PageTitle { get; set; }
        public string? Category { get; set; }
        public string? Description { get; set; }
        public string? ParentSectionName { get; set; }
        public override string ToString() => DisplayName;
    }
}
