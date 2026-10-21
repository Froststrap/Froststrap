namespace Froststrap.Models
{
    internal sealed class GradientExport
    {
        public List<GradientStops> GradientStops { get; set; } = [];
        public double? GradientAngle { get; set; }
    }
}