namespace Froststrap.Utility
{
    internal class FixedSizeList<T> : List<T>
    {
        public int MaxSize { get; }

        public FixedSizeList(int size)
        {
            MaxSize = size;
        }

        public new void Add(T item)
        {
            if (Count >= MaxSize)
                RemoveAt(Count - 1);
            base.Add(item);
        }
    }
}
