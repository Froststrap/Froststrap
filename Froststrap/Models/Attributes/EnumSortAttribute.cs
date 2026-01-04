using System;

namespace Froststrap.Models.Attributes
{
    public class EnumSortAttribute : Attribute
    {
        public int Order { get; set; }
    }
}
