using System.Collections.Generic;

namespace Unreal.ReplayLib.Models
{
    public class NetDeltaArray<T>
    {
        public ICollection<T> Items => _items.Values;
        public int Count => _items.Count;

        private readonly Dictionary<int, T> _items = new();

        public bool DeleteIndex(int index) => _items.Remove(index);

        public bool TryAddItem(int index, T item) => _items.TryAdd(index, item);

        public bool TryGetItem(int index, out T item) => _items.TryGetValue(index, out item);
    }
}