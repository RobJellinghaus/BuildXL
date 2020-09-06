// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace BuildXL.Execution.Analyzers.PackedPipGraph
{
    /// <summary>
    /// Boilerplate ID type to avoid ID confusion in code.
    /// </summary>
    public struct FileId : Id<FileId>, IEqualityComparer<FileId>
    {
        internal readonly int Value;
        internal FileId(int value) { Value = value; }
        int Id<FileId>.FromId() => Value;
        FileId Id<FileId>.ToId(int value) => new FileId(value);
        public override string ToString() => $"FileId[{Value}]";
        public bool Equals([AllowNull] FileId x, [AllowNull] FileId y) => x.Value == y.Value;
        public int GetHashCode([DisallowNull] FileId obj) => obj.Value;
    }

    public struct FileEntry 
    {
        public readonly NameId Name;
        public readonly long SizeInBytes;
        public FileEntry(NameId name, long sizeInBytes) { Name = name; SizeInBytes = sizeInBytes; }

        public struct EqualityComparer : IEqualityComparer<FileEntry>
        {
            public bool Equals(FileEntry x, FileEntry y) => x.Name.Equals(y.Name);
            public int GetHashCode([DisallowNull] FileEntry obj) => obj.Name.GetHashCode();
        }
    }

    /// <summary>
    /// Table of all files.
    /// </summary>
    /// <remarks>
    /// Every single file in an XLG trace goes into this one table.
    /// </remarks>
    public class FileTable : BaseTable<FileId, FileEntry>
    {
        public readonly NameTable FileNameTable;

        public FileTable(StringTable stringTable)
        {
            FileNameTable = new NameTable('\\', stringTable);
        }

        public class CachingBuilder : CachingBuilder<FileEntry.EqualityComparer>
        {
            public readonly NameTable.Builder NameTableBuilder;

            public CachingBuilder(FileTable table, StringTable.CachingBuilder stringTableBuilder) : base(table)
            {
                NameTableBuilder = new NameTable.Builder(table.FileNameTable, stringTableBuilder);
            }

            public FileId GetOrAdd(string filePath, long sizeInBytes)
            {
                FileEntry entry = new FileEntry(
                    NameTableBuilder.GetOrAdd(filePath),
                    sizeInBytes);
                return GetOrAdd(entry);
            }
        }
    }
}
