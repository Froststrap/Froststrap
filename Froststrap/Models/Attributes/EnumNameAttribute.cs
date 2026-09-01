namespace Froststrap.Models.Attributes
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    internal sealed class EnumNameAttribute : Attribute
    {
        public string? StaticName { get; set; }
        public string? FromTranslation { get; set; }
    }
}