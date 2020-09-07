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
        public readonly RelationTable<PipId, PipId, PipTable, PipTable> PipDependencies;

        /// <summary>
        /// The dependent relation (from the dependency, towards the dependent).
        /// </summary>
        /// <remarks>
        /// This relation is calculated as the inverse of the PipDependencies relation.
        /// </remarks>
        public readonly RelationTable<PipId, PipId, PipTable, PipTable> PipDependents;

        public PackedPipGraph()
        {
            StringTable = new StringTable();
            PipTable = new PipTable(StringTable);
            FileTable = new FileTable(StringTable);
        }

        private static readonly string s_stringTableFileName = $"{nameof(StringTable)}.txt";
        private static readonly string s_pipTableFileName = $"{nameof(PipTable)}.bin";
        private static readonly string s_fileTableFileName = $"{nameof(FileTable)}.bin";

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

            public PipId GetOrAddPip(string hash, string name, TimeSpan executionTime)
                => PipTableBuilder.GetOrAdd(hash, name, executionTime);

            public FileId GetOrAddFile(string name, long sizeInBytes)
                => FileTableBuilder.GetOrAdd(name, sizeInBytes);
        }
    }
}
