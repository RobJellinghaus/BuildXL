// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using BuildXL.Utilities.Configuration.Mutable;

namespace BuildXL.Execution.Analyzers.PackedPipGraph
{
    /// <summary>
    /// A List implementation that allows Spans to be built over its backing store.
    /// </summary>
    /// <remarks>
    /// This should clearly be in the framework
    /// </remarks>
    public class SpannableList<T> : IList<T>
        where T : unmanaged
    {
        private T[] m_elements;

        public SpannableList(int capacity = 100)
        {
            if (capacity <= 0) { throw new ArgumentException($"Capacity {capacity} must be >= 0)"); }

            m_elements = new T[capacity];
        }

        private void CheckIndex(int index)
        {
            if (index < 0) { throw new ArgumentException($"Index {index} must be >= 0"); }
            if (index >= Count) { throw new ArgumentException($"Index {index} must be < Count {Count}"); }
        }

        public T this[int index]
        {
            get
            {
                CheckIndex(index);
                return m_elements[index];
            }

            set
            {
                CheckIndex(index);
                m_elements[index] = value;
            }
        }

        public int Count { get; private set; }

        public bool IsReadOnly => false;

        private const float GrowthFactor = 1.4f; // 2 would eat too much when list gets very big

        private void EnsureCapacity(int numItems)
        {
            int nextSize = m_elements.Length;
            if (Count + numItems >= nextSize)
            {
                do
                {
                    Console.WriteLine($"SpannableList.EnsureCapacity: nextSize {nextSize}, Count + numItems {Count + numItems}");
                    nextSize = (int)(nextSize * GrowthFactor) + 1;
                }
                while (Count + numItems >= nextSize);

                T[] newElements = new T[nextSize];
                m_elements.CopyTo(newElements, 0);
                m_elements = newElements;
            }
        }

        public void Add(T item)
        {
            EnsureCapacity(1);
            if (m_elements.Length <= Count) { throw new InvalidOperationException($"SpannableList.Add: capacity {m_elements.Length}, count {Count}"); }
            m_elements[Count++] = item;
        }

        public void AddRange(IEnumerable<T> range)
        {
            foreach (T t in range)
            {
                Add(t);
            }
        }

        public void Clear()
        {
            m_elements.AsSpan(0, Count).Fill(default);
            Count = 0;
        }

        public bool Contains(T item) => IndexOf(item) != -1;

        /// <summary>
        /// Add this many more of this item.
        /// </summary>
        public void Fill(int count, T value)
        {
            int originalCount = Count;
            EnsureCapacity(count);
            m_elements.AsSpan().Slice(originalCount, count).Fill(value);
            Count += count;

            Console.WriteLine($"SpannableList.Fill: originalCount {originalCount}, count {Count}");
        }

        public int Capacity
        {
            get
            {
                return m_elements.Length;
            }
            set
            {
                if (value < Capacity) { return; } // we never shrink at the moment

                EnsureCapacity(value - Capacity);
            }
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            for (int i = 0; i < Count; i++)
            {
                array[arrayIndex + i] = this[i];
            }
        }

        private class SpannableListEnumerator : IEnumerator<T>
        {
            private readonly SpannableList<T> m_list;
            private int m_index = 0;
            internal SpannableListEnumerator(SpannableList<T> list)
            {
                m_list = list;
            }

            public T Current => m_list[m_index];

            object IEnumerator.Current => m_list[m_index];

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                m_index++;
                return m_index == m_list.Count;
            }

            public void Reset()
            {
                m_index = 0;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return new SpannableListEnumerator(this);
        }

        public int IndexOf(T item)
        {
            for (int i = 0; i < Count; i++)
            {
                T t = this[i];
                if (t.Equals(item))
                {
                    return i;
                }
            }
            return -1;
        }

        public void Insert(int index, T item)
        {
            EnsureCapacity(1);
            for (int i = Count; i > index; i--)
            {
                m_elements[i] = m_elements[i - 1];
            }
            m_elements[index] = item;
            Count++;
        }

        public bool Remove(T item)
        {
            int index = IndexOf(item);
            if (index == -1)
            {
                return false;
            }

            RemoveAt(index);
            return true;
        }

        public void RemoveAt(int index)
        {
            for (int i = index; i < Count - 1; i++)
            {
                m_elements[i] = m_elements[i + 1];
            }
            m_elements[Count - 1] = default;
            Count--;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new System.NotImplementedException();
        }

        /// <summary>
        /// The whole point of this class: get a span over the backing store.
        /// </summary>
        public Span<T> AsSpan()
        {
            return m_elements.AsSpan().Slice(0, Count);
        }

        public override string ToString()
        {
            return $"SpannableList<{typeof(T).Name}>[{Count}]";
        }

        public string ToFullString()
        {
            StringBuilder b = new StringBuilder(ToString());
            b.Append("{");
            for (int i = 0; i < Count; i++)
            {
                b.Append($" {this[i]}");
                if (i < Count - 1)
                {
                    b.Append(",");
                }
            }
            b.Append(" }");
            return b.ToString();
        }
    }
}
