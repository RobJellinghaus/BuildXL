﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace BuildXL.Execution.Analyzers.PackedTable
{
    /// <summary>
    /// Boilerplate ID type to avoid ID confusion in code.
    /// </summary>
    public struct StringId : Id<StringId>, IEqualityComparer<StringId>
    {
        public readonly int Value;
        public StringId(int value) { Id<StringId>.CheckNotZero(value); Value = value; }
        public int FromId() => Value;
        public StringId ToId(int value) => new StringId(value);
        public override string ToString() => $"StringId[{Value}]";
        public bool Equals([AllowNull] StringId x, [AllowNull] StringId y) => x.Value == y.Value;
        public int GetHashCode([DisallowNull] StringId obj) => obj.Value;
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
            private readonly Dictionary<CharSpan, StringId> m_entries;

            private readonly StringTable m_stringTable;

            internal CachingBuilder(StringTable stringTable)
            {
                m_stringTable = stringTable;
                m_entries = new Dictionary<CharSpan, StringId>(new CharSpan.EqualityComparer(stringTable));
                // Prepopulate the dictionary that does the caching
                foreach (StringId id in m_stringTable.Ids)
                {
                    m_entries.Add(new CharSpan(id), id);
                }
            }

            public StringId GetOrAdd(string s) => GetOrAdd(new CharSpan(s));

            /// <summary>
            /// Get or add this value to the StringTable.
            /// </summary>
            /// <remarks>
            /// The CharSpan type lets us refer to a slice of an underlying string (to allow splitting strings without allocating).
            /// </remarks>
            public virtual StringId GetOrAdd(CharSpan value)
            {
                if (m_entries.TryGetValue(value, out StringId id))
                {
                    return id;
                }
                else
                {
                    id = m_stringTable.Add(value.AsSpan(m_stringTable));
                    // and add the entry as a reference to our own backing store, not the one passed in 
                    // (since the value passed in is probably holding onto part of an actual string)
                    m_entries.Add(new CharSpan(id), id);
                    return id;
                }
            }
        }
    }
}
