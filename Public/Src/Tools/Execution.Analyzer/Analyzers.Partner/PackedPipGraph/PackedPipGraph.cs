// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace BuildXL.Execution.Analyzers.PackedPipGraph
{
    /// <summary>
    /// Overall data structure representing an entire BXL execution graph.
    /// </summary>
    /// <remarks>
    /// Consists of multiple tables, with methods to construct, save, and load them all.
    /// </remarks>
    public class PackedPipGraph
    {
        /// <summary>
        /// The collection of all strings from the whole graph.
        /// </summary>
        /// <remarks>
        /// Case-sensitive.
        /// </remarks>
        public readonly StringTable StringTable;

        /// <summary>
        /// The set of all pips.
        /// </summary>
        public readonly PipTable PipTable;

        /// <summary>
        /// The set of all files.
        /// </summary>
        public readonly FileTable FileTable;

        /// <summary>
        /// The dependency relation (from the dependent, towards the dependency).
        /// </summary>
        public RelationTable<PipId, PipId, PipTable, PipTable> PipDependencies { get; private set; }

        /// <summary>
        /// Construct a PackedPipGraph with empty base tables.
        /// </summary>
        /// <remarks>
        /// After creating these tables, create their Builders (inner classes) to populate them.
        /// Note that calling ConstructRelationTables() is necessary after these are fully built,
        /// before the relations can then be built.
        /// </remarks>
        public PackedPipGraph()
        {
            StringTable = new StringTable();
            PipTable = new PipTable(StringTable);
            FileTable = new FileTable(StringTable);
        }

        private static readonly string s_stringTableFileName = $"{nameof(StringTable)}.txt";
        private static readonly string s_pipTableFileName = $"{nameof(PipTable)}.bin";
        private static readonly string s_fileTableFileName = $"{nameof(FileTable)}.bin";

        /// <summary>
        /// After the base tables are populated, construct the (now properly sized) relation tables.
        /// </summary>
        public void ConstructRelationTables()
        {
            PipDependencies = new RelationTable<PipId, PipId, PipTable, PipTable>(PipTable, PipTable);
        }

        public void SaveToDirectory(string directory)
        {
            StringTable.SaveToFile(directory, s_stringTableFileName);
            PipTable.SaveToFile(directory, s_pipTableFileName);
            FileTable.SaveToFile(directory, s_fileTableFileName);
        }

        public void LoadFromDirectory(string directory)
        {
            StringTable.LoadFromFile(directory, s_stringTableFileName);
            PipTable.LoadFromFile(directory, s_pipTableFileName);
            FileTable.LoadFromFile(directory, s_fileTableFileName);
        }

        public class Builder
        {
            public readonly PackedPipGraph PipGraph;
            public readonly StringTable.CachingBuilder StringTableBuilder;
            public readonly PipTable.CachingBuilder PipTableBuilder;
            public readonly FileTable.CachingBuilder FileTableBuilder;

            public Builder(PackedPipGraph pipGraph)
            {
                PipGraph = pipGraph;
                StringTableBuilder = new StringTable.CachingBuilder(PipGraph.StringTable);
                PipTableBuilder = new PipTable.CachingBuilder(PipGraph.PipTable, StringTableBuilder);
                FileTableBuilder = new FileTable.CachingBuilder(PipGraph.FileTable, StringTableBuilder);
            }

            public PipId GetOrAddPip(string hash, string name, PipType pipType)
                => PipTableBuilder.GetOrAdd(hash, name, pipType);

            public FileId GetOrAddFile(string name, long sizeInBytes)
                => FileTableBuilder.GetOrAdd(name, sizeInBytes);
        }
    }
}
