namespace Froststrap.Models.Overlay
{
    internal record ServerAvatar(string? ImageUrl, string? OverflowText)
    {
        public bool IsOverflow => OverflowText is not null;
    }
}