// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using bxlanalyzer.Analyzers.Partner.PackedExecution;

namespace BuildXL.Execution.Analyzers.PackedExecution
{
    /// <summary>
    /// Boilerplate ID type to avoid ID confusion in code.
    /// </summary>
    public struct DirectoryId : Id<DirectoryId>, IEqualityComparer<DirectoryId>
    {
        public readonly int Value;
        public DirectoryId(int value) { Id<StringId>.CheckNotZero(value); Value = value; }
        int Id<DirectoryId>.FromId() => Value;
        DirectoryId Id<DirectoryId>.ToId(int value) => new DirectoryId(value);
        public override string ToString() => $"DirectoryId[{Value}]";
        public bool Equals([AllowNull] DirectoryId x, [AllowNull] DirectoryId y) => x.Value == y.Value;
        public int GetHashCode([DisallowNull] DirectoryId obj) => obj.Value;
    }

    /// <summary>
    /// Information about a single file.
    /// </summary>
    public struct DirectoryEntry 
    {
        /// <summary>
        /// The directory path.
        /// </summary>
        public readonly NameId Path;
        /// <summary>
        /// The producing pip.
        /// </summary>
        public readonly PipId ProducerPip;
        public readonly ContentFlags ContentFlags;

        public DirectoryEntry(NameId path, PipId producerPip, ContentFlags contentFlags)
        { 
            Path = path;
            ProducerPip = producerPip;
            ContentFlags = contentFlags;
        }

        public DirectoryEntry WithPath(NameId path) { return new DirectoryEntry(path, ProducerPip, ContentFlags); }
        public DirectoryEntry WithProducerPip(PipId producerPip) { return new DirectoryEntry(Path, producerPip, ContentFlags); }
        public DirectoryEntry WithContentFlags(ContentFlags contentFlags) { return new DirectoryEntry(Path, ProducerPip, contentFlags); }

        public struct EqualityComparer : IEqualityComparer<DirectoryEntry>
        {
            public bool Equals(DirectoryEntry x, DirectoryEntry y) => x.Path.Equals(y.Path);
            public int GetHashCode([DisallowNull] DirectoryEntry obj) => obj.Path.GetHashCode();
        }
    }

    /// <summary>
    /// Table of all files.
    /// </summary>
    /// <remarks>
    /// Every single file in an XLG trace goes into this one table.
    /// </remarks>
    public class DirectoryTable : SingleValueTable<DirectoryId, DirectoryEntry>
    {
        /// <summary>
        /// The names of files in this DirectoryTable.
        /// </summary>
        /// <remarks>
        /// This sub-table is owned by this DirectoryTable; the DirectoryTable constructs it, and saves and loads it.
        /// </remarks>
        public readonly NameTable PathTable;

        public DirectoryTable(NameTable pathTable, int capacity = DefaultCapacity) : base(capacity)
        {
            PathTable = pathTable;
        }

        public class CachingBuilder : CachingBuilder<DirectoryEntry.EqualityComparer>
        {
            public readonly NameTable.Builder PathTableBuilder;

            public CachingBuilder(DirectoryTable table, NameTable.Builder pathTableBuilder) : base(table)
            {
                PathTableBuilder = pathTableBuilder;
            }

            /// <summary>
            /// Get or add an entry for the given file path.
            /// </summary>
            /// <remarks>
            /// If the entry already exists, the sizeInBytes value passed here will be ignored!
            /// The only time that value can be set is when adding a new file not previously recorded.
            /// TODO: consider failing if this happens?
            /// </remarks>
            public DirectoryId GetOrAdd(string directoryPath, PipId producerPip, ContentFlags contentFlags)
            {
                DirectoryEntry entry = new DirectoryEntry(PathTableBuilder.GetOrAdd(directoryPath), producerPip, contentFlags);
                return GetOrAdd(entry);
            }
        }
    }
}
