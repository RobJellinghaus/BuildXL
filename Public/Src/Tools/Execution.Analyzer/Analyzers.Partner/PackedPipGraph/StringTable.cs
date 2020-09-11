// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace BuildXL.Execution.Analyzers.PackedPipGraph
{
    /// <summary>
    /// Boilerplate ID type to avoid ID confusion in code.
    /// </summary>
    public struct StringId : Id<StringId>, IEqualityComparer<StringId>
    {
        public readonly int Value;
        public StringId(int value) { Value = value; }
        int Id<StringId>.FromId() => Value;
        StringId Id<StringId>.ToId(int value) => new StringId(value);
        public override string ToString() => $"StringId[{Value}]";
        public bool Equals([AllowNull] StringId x, [AllowNull] StringId y) => x.Value == y.Value;
        public int GetHashCode([DisallowNull] StringId obj) => obj.Value;
    }

    public struct StringComparerNonNull : IEqualityComparer<string>
    {
        public bool Equals([AllowNull] string x, [AllowNull] string y) => StringComparer.InvariantCulture.Equals(x, y);
        public int GetHashCode([DisallowNull] string obj) => StringComparer.InvariantCulture.GetHashCode(obj);
    }

    /// <summary>
    /// Table of unique strings.
    /// </summary>
    /// <remarks>
    /// For efficiency and to reduce code complexity, this is treated as a MultiValueTable where each ID is associated
    /// with a ReadOnlySpan[char].
    /// 
    /// To allow the contents to be readable when directly loaded from disk in a text editor, the table internally pads
    /// every entry with newline characters.
    /// </remarks>
    public class StringTable : MultiValueTable<StringId, char>
    {
        /// <summary>
        /// Not the most efficient plan, but: actually append newlines to every addition, via copying through this buffer.
        /// </summary>
        private SpannableList<char> m_buffer = new SpannableList<char>(DefaultCapacity);

        public StringTable(int capacity = DefaultCapacity) : base(capacity)
        {
        }

        private ReadOnlySpan<char> AppendNewline(ReadOnlySpan<char> chars)
        {
            m_buffer.Clear();
            m_buffer.Fill(chars.Length + Environment.NewLine.Length, default);
            chars.CopyTo(m_buffer.AsSpan());
            Environment.NewLine.AsSpan().CopyTo(m_buffer.AsSpan().Slice(chars.Length));
            return m_buffer.AsSpan();
        }

        public override StringId Add(ReadOnlySpan<char> multiValues) => base.Add(AppendNewline(multiValues));

        public override ReadOnlySpan<char> this[StringId id] 
        {
            get
            {
                ReadOnlySpan<char> text = base[id];
                // splice the newline back out so clients never see it
                return text.Slice(0, text.Length - Environment.NewLine.Length);
            }
            set => throw new ArgumentException($"Cannot set text of {id}; strings are immutable once added to StringTable");
        }

        /// <summary>
        /// Build a SingleValueTable which caches items by hash value, adding any item only once.
        /// </summary>
        public class CachingBuilder
        {
            /// <summary>
            /// Efficient lookup by hash value.
            /// </summary>
            /// <remarks>
            /// This is really only necessary when building the table, and should probably be split out into a builder type.
            /// </remarks>
            private readonly Dictionary<string, StringId> m_entries = new Dictionary<string, StringId>();

            private readonly MultiValueTable<StringId, char> m_valueTable;

            internal CachingBuilder(MultiValueTable<StringId, char> valueTable)
            {
                m_valueTable = valueTable;
                // Prepopulate the dictionary that does the caching
                foreach (StringId id in m_valueTable.Ids)
                {
                    m_entries.Add(new string(m_valueTable[id]), id);
                }
            }

            public virtual StringId GetOrAdd(ReadOnlySpan<char> value)
            {
                // Making strings here is quite expensive, but avoiding it would require building some kind of
                // span-based dictionary, and this will work correctly and be sufficiently fast.
                string valueString = new string(value);
                if (m_entries.TryGetValue(valueString, out StringId id))
                {
                    return id;
                }
                else
                {
                    id = m_valueTable.Add(value);
                    m_entries.Add(valueString, id);
                    return id;
                }
            }
        }
    }
}
