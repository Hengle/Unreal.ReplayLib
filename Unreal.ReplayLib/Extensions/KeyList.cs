using System;
using System.Collections.Generic;
using System.Linq;

namespace Unreal.ReplayLib.Extensions
{
    public class KeyList<TK, TV>
    {
        private readonly List<TV> _vals = new();
        private readonly Dictionary<TK, int> _keys = new();
        
        public int Length => _vals.Count;
        public int Count(Func<TV, bool> func) => Values.Count(func);

        public void Add(TK key, TV val)
        {
            if (_keys.TryAdd(key, _vals.Count))
            {
                _vals.Add(val);
            }
        }

        public List<TV> Values => _vals;

        public bool TryGetIndex(TK key, out int index) => _keys.TryGetValue(key, out index);

        public bool TryGetValue(int keyId, out TV val)
        {
            val = default;

            if (keyId >= 0 && keyId < _vals.Count)
            {
                val = _vals[keyId];

                return true;
            }

            return false;
        }

        public bool TryGetValue(TK key, out TV val)
        {
            if (_keys.TryGetValue(key, out var id))
            {
                val = _vals[id];

                return true;
            }

            val = default;

            return false;
        }

        public TV this[int index] => _vals[index];

        public Dictionary<TKey, TElement> ToDictionary<TKey, TElement>(Func<TK, TKey> keyFunc,
            Func<TV, TElement> valFunc)
        {
            var vals = new Dictionary<TKey, TElement>();
            foreach (var (key, value) in _keys)
            {
                vals.Add(keyFunc(key), valFunc(_vals[value]));
            }

            return vals;
        }
    }
}