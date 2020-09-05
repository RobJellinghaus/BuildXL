// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
                    prefixId = GetOrAdd(new NameEntry(prefixId, atomId));
                }
                return prefixId;
            }
        }
    }
}
