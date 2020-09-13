﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using BuildXL.Utilities;

namespace BuildXL.Execution.Analyzers.PackedExecution
{
    /// <summary>
    /// Boilerplate ID type to avoid ID confusion in code.
    /// </summary>
    public struct FileId : Id<FileId>, IEqualityComparer<FileId>
    {
        public readonly int Value;
        public FileId(int value) { Id<FileId>.CheckNotZero(value); Value = value; }
        int Id<FileId>.FromId() => Value;
        FileId Id<FileId>.ToId(int value) => new FileId(value);
        public override string ToString() => $"FileId[{Value}]";
        public bool Equals([AllowNull] FileId x, [AllowNull] FileId y) => x.Value == y.Value;
        public int GetHashCode([DisallowNull] FileId obj) => obj.Value;
    }

    /// <summary>
    /// Information about a single file.
    /// </summary>
    public struct FileEntry 
    {
        /// <summary>
        /// The file
        /// </summary>
        public readonly NameId Path;
        public readonly long SizeInBytes;
        public readonly PipId ProducerPip;
        public FileEntry(NameId name, long sizeInBytes, PipId producerPip)
        { 
            Path = name;
            SizeInBytes = sizeInBytes;
            ProducerPip = producerPip;
        }

        public FileEntry WithName(NameId name) { return new FileEntry(name, SizeInBytes, ProducerPip); }
        public FileEntry WithSizeInBytes(long sizeInBytes) { return new FileEntry(Path, sizeInBytes, ProducerPip); }
        public FileEntry WithProducerPip(PipId producerPip) { return new FileEntry(Path, SizeInBytes, producerPip); }

        public struct EqualityComparer : IEqualityComparer<FileEntry>
        {
            public bool Equals(FileEntry x, FileEntry y) => x.Path.Equals(y.Path);
            public int GetHashCode([DisallowNull] FileEntry obj) => obj.Path.GetHashCode();
        }
    }

    /// <summary>
    /// Table of all files.
    /// </summary>
    /// <remarks>
    /// Every single file in an XLG trace goes into this one table.
    /// </remarks>
    public class FileTable : SingleValueTable<FileId, FileEntry>
    {
        /// <summary>
        /// The names of files in this FileTable.
        /// </summary>
        /// <remarks>
        /// This table is shared between this table and the DirectoryTable.
        /// </remarks>
        public readonly NameTable PathTable;

        public FileTable(NameTable pathTable, int capacity = DefaultCapacity) : base(capacity)
        {
            PathTable = pathTable;
        }

        public class CachingBuilder : CachingBuilder<FileEntry.EqualityComparer>
        {
            public readonly NameTable.Builder PathTableBuilder;

            public CachingBuilder(FileTable table, NameTable.Builder pathTableBuilder) : base(table)
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
            public FileId GetOrAdd(string filePath, long sizeInBytes, PipId producerPip)
            {
                FileEntry entry = new FileEntry(
                    PathTableBuilder.GetOrAdd(filePath),
                    sizeInBytes,
                    producerPip);
                return GetOrAdd(entry);
            }
        }
    }
}
