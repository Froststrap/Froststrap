using System;

namespace Froststrap.Models.Attributes
{
    public class EnumNameAttribute : Attribute
    {
        public string? StaticName { get; set; }
        public string? FromTranslation { get; set; }
    }
}
