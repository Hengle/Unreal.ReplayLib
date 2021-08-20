namespace Unreal.ReplayLib.Extensions
{
    public sealed class FastClearArray<T>
    {
        public int Count { get; private set; }
        private readonly T[] _array;

        public FastClearArray(int maxCapacity) => _array = new T[maxCapacity];

        public void Add(T item)
        {
            _array[Count] = item;
            Count++;
        }

        public void Clear() => Count = 0;

        public T this[int index]
        {
            get => _array[index];
            set => _array[index] = value;
        }
    }
}