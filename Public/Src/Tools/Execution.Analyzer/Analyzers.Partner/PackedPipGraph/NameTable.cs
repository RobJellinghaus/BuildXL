﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using BuildXL.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace BuildXL.Execution.Analyzers.PackedPipGraph
{
    /// <summary>
    /// Boilerplate ID type to avoid ID confusion in code.
    /// </summary>
    public struct NameId : Id<NameId>
    {
        internal readonly int Value;
        internal NameId(int value) { Id<NameId>.CheckNotZero(value); Value = value; }
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

        public struct EqualityComparer : IEqualityComparer<NameEntry>
        {
            public bool Equals(NameEntry x, NameEntry y) => x.Prefix.Equals(y.Prefix) && x.Atom.Equals(y.Atom);
            public int GetHashCode([DisallowNull] NameEntry obj) =>
                HashCodeHelper.Combine(obj.Prefix.GetHashCode(), obj.Atom.GetHashCode());
        }
    }

    /// <summary>
    /// Suffix table representation of sequential names, such as pip names or file paths.
    /// </summary>
    /// <remarks>
    /// This representation shares all sub-names as much as possible. For example, for a NameTable with
    /// period delimiters that is initially empty:
    /// 
    /// - Storing the name "a.b.c" will result in three names in the table: "a", "a.b", and "a.b.c".
    /// - Each name is represented by a pointer to its prefix, and the atom at the end.
    /// - If we then store "a.b.d", only one additional name will be added, because the prefix "a.b" will be shared.
    /// 
    /// The intent is to optimize the representation of long, sequential names which have many repeated subparts.
    /// Both file paths and pip names fit this description, and this type is used for both.
    /// </remarks>
    public class NameTable : SingleValueTable<NameId, NameEntry>
    {
        /// <summary>
        /// The separator between parts of names in this table.
        /// </summary>
        /// <remarks>
        /// Typically either '.' or '\\' or '/'
        /// 
        /// Note that this is not part of the persistent state of the table; this is provided at construction, and
        /// the code doing the construction should know what type of separator is needed.
        /// </remarks>
        public readonly char Separator;

        /// <summary>
        /// The backing string table used by names in this table; not owned by this table, may be shared (and probably is).
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
                // Walk up the prefix chain to the end.
                entry = this[id];
                if (entry.Atom.Equals(default)) { throw new Exception($"Invalid atom for id {entry.Atom}"); }

                // Are we at the end yet?
                atEnd = entry.Prefix.Equals(default);

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
            if (!entry.Prefix.Equals(default))
            {
                // recurse on the prefix, which will result in it getting written into the first part of span
                prefixSpan = GetText(entry.Prefix, span);
                // add the separator
                span[prefixSpan.Length] = Separator;

                prefixSpan = span.Slice(0, prefixSpan.Length + 1);
            }
            else
            {
                // we're at the start -- base case of the recursion
                prefixSpan = span.Slice(0, 0);
            }
            ReadOnlySpan<char> atom = StringTable[entry.Atom];
            atom.CopyTo(span.Slice(prefixSpan.Length));
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

        public class Builder : CachingBuilder<NameEntry.EqualityComparer>
        {
            public Builder(NameTable table, StringTable.CachingBuilder stringTableBuilder) : base(table) 
            {
                StringTableBuilder = stringTableBuilder;
            }

            private NameTable NameTable => (NameTable)ValueTable;
            public readonly StringTable.CachingBuilder StringTableBuilder;

            private static bool CharComparer(char c1, char c2) => c1 == c2;

            /// <summary>
            /// Split this string into its constituent pieces and ensure it exists as a Name.
            /// </summary>
            /// <remarks>
            /// This is not very efficient since it uses string.Split rather than something Span-based,
            /// but all the allocations are temporary, so not optimizing it... yet.
            /// </remarks>
            public NameId GetOrAdd(string s)
            {
                NameId prefixId = default;
                ReadOnlySpan<char> span = s.AsSpan();
                // Index relative to the start of the whole string.
                int start = 0;
                while (span.Length > 0)
                {
                    // Get the next atom (without allocating).
                    ReadOnlySpan<char> nextAtom = span.SplitPrefix(NameTable.Separator, CharComparer);

                    // Look up the next atom and add it to the string table
                    // (allocating only if we're expanding the string table's backing store).
                    StringId atomId = StringTableBuilder.GetOrAdd(new CharSpan(s, start, nextAtom.Length));

                    // if this prefix/atom pair already exists, we will get the ID of the current version,
                    // hence sharing it. Otherwise, we'll make a new entry and get a new ID for it.
                    // Either way, we'll then iterate, using the ID (current or new) as the prefix for
                    // the next piece.
                    prefixId = GetOrAdd(new NameEntry(prefixId, atomId));

                    // Advance span past the separator (if we're not at the end).
                    int nextIndex = nextAtom.Length == span.Length ? nextAtom.Length : nextAtom.Length + 1;
                    span = span.Slice(nextIndex);
                    start += nextIndex;
                }

                // The ID we wind up with is the ID of the entire name.
                return prefixId;
            }
        }
    }
}
