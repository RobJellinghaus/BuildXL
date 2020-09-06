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
        public readonly StringTable StringTable;
        public readonly PipTable PipTable;
        public readonly FileTable FileTable;

        public PackedPipGraph()
        {
            StringTable = new StringTable();
            PipTable = new PipTable(StringTable);
            FileTable = new FileTable(StringTable);
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
