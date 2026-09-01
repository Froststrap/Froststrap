namespace Froststrap.Models.Attributes
{
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    internal sealed class EnumSortAttribute : Attribute
    {
        public int Order { get; set; }
    }
}