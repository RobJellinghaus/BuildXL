// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace BuildXL.Execution.Analyzers.PackedExecution
{
    /// <summary>
    /// Boilerplate ID type to avoid ID confusion in code.
    /// </summary>
    public struct FileId : Id<FileId>, IEqualityComparer<FileId>
    {
        public readonly int Value;
        public FileId(int value) { Id<StringId>.CheckNotZero(value); Value = value; }
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
        /// The file path.
        /// </summary>
        public readonly NameId Path;
        /// <summary>
        /// The file size.
        /// </summary>
        public readonly long SizeInBytes;
        /// <summary>
        /// The producing pip.
        /// </summary>
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
        /// This sub-table is owned by this FileTable; the FileTable constructs it, and saves and loads it.
        /// </remarks>
        public readonly NameTable FileNameTable;

        public FileTable(StringTable stringTable, int capacity = DefaultCapacity) : base(capacity)
        {
            FileNameTable = new NameTable('\\', stringTable);
        }

        public override void SaveToFile(string directory, string name)
        {
            base.SaveToFile(directory, name);
            FileNameTable.SaveToFile(directory, InsertSuffix(name, nameof(FileNameTable)));
        }

        public override void LoadFromFile(string directory, string name)
        {
            base.LoadFromFile(directory, name);
            FileNameTable.LoadFromFile(directory, InsertSuffix(name, nameof(FileNameTable)));
        }

        public class CachingBuilder : CachingBuilder<FileEntry.EqualityComparer>
        {
            public readonly NameTable.Builder NameTableBuilder;

            public CachingBuilder(FileTable table, StringTable.CachingBuilder stringTableBuilder) : base(table)
            {
                NameTableBuilder = new NameTable.Builder(table.FileNameTable, stringTableBuilder);
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
                FileEntry entry = new FileEntry(NameTableBuilder.GetOrAdd(filePath), sizeInBytes, producerPip);
                return GetOrAdd(entry);
            }
        }
    }
}
