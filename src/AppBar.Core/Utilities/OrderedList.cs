using System;
using System.Collections.Generic;

namespace AppBar.Core.Utilities
{
    public static class OrderedList
    {
        public static bool Move<T>(IList<T> items, int oldIndex, int newIndex)
        {
            if (items == null || oldIndex < 0 || oldIndex >= items.Count)
            {
                return false;
            }

            if (newIndex < 0)
            {
                newIndex = 0;
            }

            if (newIndex >= items.Count)
            {
                newIndex = items.Count - 1;
            }

            if (newIndex == oldIndex)
            {
                return false;
            }

            T item = items[oldIndex];
            items.RemoveAt(oldIndex);
            items.Insert(newIndex, item);
            return true;
        }
    }
}

