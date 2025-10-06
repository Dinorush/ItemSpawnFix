using System.Collections;
using System.Collections.Generic;

namespace ItemSpawnFix.Utils
{
    // Iterates over each element in each list held by a dictionary's values.
    internal class DictListEnumerator<T> : IEnumerator<T>
    {
        private readonly IEnumerator<List<T>> _valueEnumerator;
        private List<T>? _curList;
        private int _index;
        private T? _current;

        internal DictListEnumerator(Dictionary<string, List<T>> data)
        {
            _valueEnumerator = data.Values.GetEnumerator();
            if (_valueEnumerator.MoveNext())
                _curList = _valueEnumerator.Current;
            _index = -1;
        }

        public T Current => _current;

        object IEnumerator.Current => Current;

        public void Dispose() { }

        public bool MoveNext()
        {
            if (_curList == null || ++_index >= _curList.Count)
            {
                if (!_valueEnumerator.MoveNext())
                    return false;
                else
                {
                    _curList = _valueEnumerator.Current;
                    _index = 0;
                }
            }

            _current = _curList[_index];

            return true;
        }

        public void Reset()
        {
            _valueEnumerator.Reset();
            if (_valueEnumerator.MoveNext())
                _curList = _valueEnumerator.Current;
            _index = -1;
        }
    }
}
