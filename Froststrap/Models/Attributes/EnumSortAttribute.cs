namespace Froststrap.Models.Attributes
{
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    internal class EnumSortAttribute : Attribute
    {
        public int Order { get; set; }
    }
}