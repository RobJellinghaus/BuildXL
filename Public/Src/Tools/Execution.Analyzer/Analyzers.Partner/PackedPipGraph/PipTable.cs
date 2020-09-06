// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace BuildXL.Execution.Analyzers.PackedPipGraph
{
    /// <summary>
    /// Boilerplate ID type to avoid ID confusion in code.
    /// </summary>
    public struct PipId : Id<PipId>, IEqualityComparer<PipId>
    {
        internal readonly int Value;
        internal PipId(int value) { Value = value; }
        int Id<PipId>.FromId() => Value;
        PipId Id<PipId>.ToId(int value) => new PipId(value);
        public override string ToString() => $"PipId[{Value}]";
        public bool Equals([AllowNull] PipId x, [AllowNull] PipId y) => x.Value == y.Value;
        public int GetHashCode([DisallowNull] PipId obj) => obj.Value;
    }

    public struct PipEntry
    {
        public readonly StringId Hash;
        public readonly NameId Name;
        // TODO: do we want ExecutionTime to be in a DerivedTable? (we may, if we want to experiment with it)
        public readonly TimeSpan ExecutionTime;
        public PipEntry(StringId hash, NameId name, TimeSpan executionTime) { Hash = hash; Name = name; ExecutionTime = executionTime; }

        public struct EqualityComparer : IEqualityComparer<PipEntry>
        {
            public bool Equals(PipEntry x, PipEntry y) => x.Hash.Equals(y.Hash);
            public int GetHashCode([DisallowNull] PipEntry obj) => obj.Hash.GetHashCode();
        }
    }

    /// <summary>
    /// Table of pip data.
    /// </summary>
    public class PipTable : BaseUnmanagedTable<PipId, PipEntry>
    {
        /// <summary>
        /// The names of pips in this table.
        /// </summary>
        /// <remarks>
        /// This sub-table is owned by this PipTable; the PipTable constructs it, and saves and loads it.
        /// </remarks>
        public readonly NameTable PipNameTable;

        public PipTable(StringTable stringTable, int capacity = -1) : base(capacity)
        {
            PipNameTable = new NameTable('.', stringTable);
        }

        public override void SaveToFile(string directory, string name)
        {
            base.SaveToFile(directory, name);
            PipNameTable.SaveToFile(directory, $"{nameof(PipNameTable)}.{name}");
        }

        public override void LoadFromFile(string directory, string name)
        {
            base.LoadFromFile(directory, name);
            PipNameTable.LoadFromFile(directory, $"{nameof(PipNameTable)}.{name}");
        }

        public class CachingBuilder : CachingBuilder<PipEntry.EqualityComparer>
        {
            public readonly NameTable.Builder NameTableBuilder;

            public CachingBuilder(PipTable table, StringTable.CachingBuilder stringTableBuilder) : base(table)
            {
                NameTableBuilder = new NameTable.Builder(table.PipNameTable, stringTableBuilder);
            }

            public PipId GetOrAdd(string hash, string pipName, TimeSpan executionTime)
            {
                PipEntry entry = new PipEntry(
                    NameTableBuilder.StringTableBuilder.GetOrAdd(hash),
                    NameTableBuilder.GetOrAdd(pipName),
                    executionTime);
                return GetOrAdd(entry);
            }
        }
    }
}
