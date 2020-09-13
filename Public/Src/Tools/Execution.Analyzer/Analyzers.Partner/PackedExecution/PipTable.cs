// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace BuildXL.Execution.Analyzers.PackedExecution
{
    /// <summary>
    /// Enumeration representing the types of pips.
    /// </summary>
    /// <remarks>
    /// This is very debatable, copying this from BuildXL\Public\Src\Pips\Dll\Operations\PipType.cs.
    /// Pro: having this in the separate library means the library is standalone and doesn't need
    /// pieces of BXL itself. Con: obvious duplication and code drift. Solution: TBD.
    /// </remarks>
    public enum PipType : byte
    {
        /// <summary>
        /// A write file pip.
        /// </summary>
        WriteFile,

        /// <summary>
        /// A copy file pip.
        /// </summary>
        CopyFile,

        /// <summary>
        /// A process pip.
        /// </summary>
        Process,

        /// <summary>
        /// A pip representing an IPC call (to some other service pip)
        /// </summary>
        Ipc,

        /// <summary>
        /// A value pip
        /// </summary>
        Value,

        /// <summary>
        /// A spec file pip
        /// </summary>
        SpecFile,

        /// <summary>
        /// A module pip
        /// </summary>
        Module,

        /// <summary>
        /// A pip representing the hashing of a source file
        /// </summary>
        HashSourceFile,

        /// <summary>
        /// A pip representing the completion of a directory (after which it is immutable).
        /// </summary>
        SealDirectory,

        /// <summary>
        /// This is a non-value, but places an upper-bound on the range of the enum
        /// </summary>
        Max,
    }

    /// <summary>
    /// Boilerplate ID type to avoid ID confusion in code.
    /// </summary>
    public readonly struct PipId : Id<PipId>, IEqualityComparer<PipId>
    {
        public readonly int Value;
        public PipId(int value) { Id<StringId>.CheckNotZero(value); Value = value; }
        int Id<PipId>.FromId() => Value;
        PipId Id<PipId>.ToId(int value) => new PipId(value);
        public override string ToString() => $"PipId[{Value}]";
        public bool Equals([AllowNull] PipId x, [AllowNull] PipId y) => x.Value == y.Value;
        public int GetHashCode([DisallowNull] PipId obj) => obj.Value;
    }

    public struct PipEntry
    {
        /// <summary>
        /// Semi-stable hash.
        /// </summary>
        public readonly long SemiStableHash;

        /// <summary>
        /// Full name.
        /// </summary>
        public readonly NameId Name;

        /// <summary>
        /// Pip type.
        /// </summary>
        public readonly PipType PipType;

        public PipEntry(
            long semiStableHash,
            NameId name,
            PipType type)
        { 
            SemiStableHash = semiStableHash; 
            Name = name;
            PipType = type;
        }

        /// <summary>
        /// Compare PipEntries by SemiStableHash value.
        /// </summary>
        public struct EqualityComparer : IEqualityComparer<PipEntry>
        {
            public bool Equals(PipEntry x, PipEntry y) => x.SemiStableHash.Equals(y.SemiStableHash);
            public int GetHashCode([DisallowNull] PipEntry obj) => obj.SemiStableHash.GetHashCode();
        }
    }

    /// <summary>
    /// Table of pip data.
    /// </summary>
    public class PipTable : SingleValueTable<PipId, PipEntry>
    {
        /// <summary>
        /// The names of pips in this table.
        /// </summary>
        /// <remarks>
        /// This sub-table is owned by this PipTable; the PipTable constructs it, and saves and loads it.
        /// </remarks>
        public readonly NameTable PipNameTable;

        public PipTable(StringTable stringTable, int capacity = DefaultCapacity) : base(capacity)
        {
            PipNameTable = new NameTable('.', stringTable);
        }

        public override void SaveToFile(string directory, string name)
        {
            base.SaveToFile(directory, name);
            PipNameTable.SaveToFile(directory, InsertSuffix(name, nameof(PipNameTable)));
        }

        public override void LoadFromFile(string directory, string name)
        {
            base.LoadFromFile(directory, name);
            PipNameTable.LoadFromFile(directory, InsertSuffix(name, nameof(PipNameTable)));
        }

        public string PipName(PipId pipId)
        {
            return PipNameTable.GetText(this[pipId].Name);
        }

        public class Builder : CachingBuilder<PipEntry.EqualityComparer>
        {
            public readonly NameTable.Builder NameTableBuilder;

            public Builder(PipTable table, StringTable.CachingBuilder stringTableBuilder) : base(table)
            {
                NameTableBuilder = new NameTable.Builder(table.PipNameTable, stringTableBuilder);
            }

            /// <summary>
            /// Add a pip, when you are sure it's not already in the table.
            /// </summary>
            public PipId Add(long semiStableHash, string name, PipType pipType)
            {
                PipEntry entry = new PipEntry(
                    semiStableHash,
                    NameTableBuilder.GetOrAdd(name),
                    pipType);

                return ((PipTable)ValueTable).Add(entry);
            }
        }
    }
}
