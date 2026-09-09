// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Utility
{
    internal class FixedSizeList<T>(int size) : List<T>
    {
        public int MaxSize { get; } = size;

        public new void Add(T item)
        {
            if (Count >= MaxSize)
                RemoveAt(Count - 1);

            base.Add(item);
        }
    }
}
