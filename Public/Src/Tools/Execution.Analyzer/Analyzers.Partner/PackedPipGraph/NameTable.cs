// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using BuildXL.Storage.Fingerprints;
using BuildXL.Utilities;

namespace BuildXL.Execution.Analyzers.PackedPipGraph
{
    /// <summary>
    /// Boilerplate ID type to avoid ID confusion in code.
    /// </summary>
    public struct NameId : Id<NameId>
    {
        internal readonly int Value;
        internal NameId(int value) { Value = value; }
        int Id<NameId>.FromId() => Value;
        NameId Id<NameId>.ToId(int value) => new NameId(value);
        public bool Equals(NameId other) => Value == other.Value;
        public override int GetHashCode() => Value;
        public override string ToString() => $"NameId[{Value}]";
    }

    /// <summary>
    /// Suffix tree representation, where all prefixes are maximally shared.
    /// </summary>
    public struct NameEntry
    {
        public readonly NameId Prefix;
        public readonly StringId Atom;
        public NameEntry(NameId prefix, StringId atom) { Prefix = prefix; Atom = atom; }
        public override int GetHashCode() => HashCodeHelper.Combine(Prefix.GetHashCode(), Atom.GetHashCode());

        public struct EqualityComparer : IEqualityComparer<NameEntry>
        {
            public bool Equals(NameEntry x, NameEntry y) => x.Prefix.Equals(y.Prefix) && x.Atom.Equals(y.Atom);
            public int GetHashCode([DisallowNull] NameEntry obj) => obj.GetHashCode();
        }
    }

    /// <summary>
    /// Suffix table representation of sequential names, such as pip names or file paths.
    /// </summary>
    public class NameTable : BaseTable<NameId, NameEntry>
    {
        /// <summary>
        /// The separator between parts of names in this table.
        /// </summary>
        /// <remarks>
        /// Typically either '.' or '\\'
        /// </remarks>
        public readonly char Separator;

        /// <summary>
        /// The backing string table used by names in this table.
        /// </summary>
        public readonly StringTable StringTable;

        public NameTable(char separator, StringTable stringTable)
        {
            Separator = separator;
            StringTable = stringTable;
        }

        /// <summary>
        /// Length in characters of the given name.
        /// </summary>
        public int Length(NameId id)
        {
            int len = 0;
            bool atEnd = false;

            NameEntry entry;
            while (!atEnd)
            {
                entry = this[id];
                Console.WriteLine($"Got entry {entry.Prefix},{entry.Atom}");
                if (entry.Atom.Equals(default)) { throw new Exception($"Invalid atom for id {entry.Atom}"); }

                atEnd = entry.Prefix.Equals(default);
                Console.WriteLine($"At end: {atEnd}");

                len += StringTable[entry.Atom].Length;

                if (!atEnd)
                {
                    len++;
                    id = entry.Prefix;
                }
            }

            return len;
        }

        /// <summary>
        /// Get the full text of the given name, writing into the given span, returning the prefix of the span
        /// containing the full name.
        /// </summary>
        /// <remarks>
        /// Use this in hot paths when string allocation is undesirable.
        /// </remarks>
        public ReadOnlySpan<char> GetText(NameId nameId, Span<char> span)
        {
            NameEntry entry = this[nameId];
            ReadOnlySpan<char> prefixSpan;
            if (!entry.Prefix.Equals(default(NameId)))
            {
                prefixSpan = GetText(entry.Prefix, span);
                span[prefixSpan.Length] = Separator;
                prefixSpan = span.Slice(0, prefixSpan.Length + 1);
            }
            else
            {
                prefixSpan = span.Slice(0, 0);
            }
            string atom = StringTable[entry.Atom];
            atom.AsSpan().CopyTo(span.Slice(prefixSpan.Length));
            return span.Slice(0, prefixSpan.Length + atom.Length);
        }

        /// <summary>
        /// Get the full text of the given name, allocating a new string for it.
        /// </summary>
        /// <remarks>
        /// This allocates not only a string but a StringBuilder; do not use in hot paths.
        /// </remarks>
        public string GetText(NameId nameId, int capacity = 1000)
        {
            char[] buf = new char[capacity];
            Span<char> span = new Span<char>(buf);
            ReadOnlySpan<char> textSpan = GetText(nameId, span);
            return new string(textSpan);
        }

        public new class Builder : BaseTable<NameId, NameEntry>.Builder
        {
            public Builder(NameTable table, StringTable.Builder stringTableBuilder) : base(table) 
            {
                m_stringTableBuilder = stringTableBuilder;
            }

            private NameTable NameTable => (NameTable)BaseTable;
            private readonly StringTable.Builder m_stringTableBuilder;

            /// <summary>
            /// Split this string into its constituent pieces and ensure it exists as a Name.
            /// </summary>
            public NameId GetOrAdd(string s)
            {
                string[] pieces = s.Split(NameTable.Separator, System.StringSplitOptions.RemoveEmptyEntries);

                NameId prefixId = default(NameId);
                foreach (string p in pieces)
                {
                    StringId atomId = m_stringTableBuilder.GetOrAdd(p);
                    // if this prefix/atom pair already exists, we will get the ID of the current version,
                    // hence sharing it. Otherwise, we'll make a new entry and get a new ID for it.
                    // Either way, we'll then iterate, using the ID (current or new) as the prefix for
                    // the next piece.
                    prefixId = GetOrAdd(new NameEntry(prefixId, atomId));
                }
                // The ID we wind up with is the ID of the entire name.
                return prefixId;
            }
        }
    }
}
