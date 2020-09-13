// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.IO;

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
        /// The strings (all of them, from everything).
        /// </summary>
        /// <remarks>
        /// Case-sensitive.
        /// </remarks>
        public readonly StringTable StringTable;

        /// <summary>
        /// The pips.
        /// </summary>
        public readonly PipTable PipTable;

        /// <summary>
        /// The files.
        /// </summary>
        public readonly FileTable FileTable;

        /// <summary>
        /// The workers.
        /// </summary>
        public readonly WorkerTable WorkerTable;

        /// <summary>
        /// The dependency relation (from the dependent, towards the dependency).
        /// </summary>
        public RelationTable<PipId, PipId> PipDependencies { get; private set; }

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
            WorkerTable = new WorkerTable(StringTable);
        }

        private static readonly string s_stringTableFileName = $"{nameof(StringTable)}.bin";
        private static readonly string s_pipTableFileName = $"{nameof(PipTable)}.bin";
        private static readonly string s_fileTableFileName = $"{nameof(FileTable)}.bin";
        private static readonly string s_workerTableFileName = $"{nameof(WorkerTable)}.bin";
        private static readonly string s_pipDependenciesFileName = $"{nameof(PipDependencies)}.bin";

        /// <summary>
        /// After the base tables are populated, construct the (now properly sized) relation tables.
        /// </summary>
        public void ConstructRelationTables()
        {
            PipDependencies = new RelationTable<PipId, PipId>(PipTable, PipTable);
        }

        public void SaveToDirectory(string directory)
        {
            StringTable.SaveToFile(directory, s_stringTableFileName);
            PipTable.SaveToFile(directory, s_pipTableFileName);
            FileTable.SaveToFile(directory, s_fileTableFileName);
            WorkerTable.SaveToFile(directory, s_workerTableFileName);

            if (PipDependencies != null)
            {
                PipDependencies.SaveToFile(directory, s_pipDependenciesFileName);
            }
        }

        public void LoadFromDirectory(string directory)
        {
            StringTable.LoadFromFile(directory, s_stringTableFileName);
            PipTable.LoadFromFile(directory, s_pipTableFileName);
            FileTable.LoadFromFile(directory, s_fileTableFileName);
            WorkerTable.LoadFromFile(directory, s_workerTableFileName);

            if (File.Exists(Path.Combine(directory, s_pipDependenciesFileName)))
            {
                ConstructRelationTables();
                PipDependencies.LoadFromFile(directory, s_pipDependenciesFileName);
            }
        }

        public class Builder
        {
            public readonly PackedPipGraph PipGraph;
            public readonly StringTable.CachingBuilder StringTableBuilder;
            public readonly PipTable.Builder PipTableBuilder;
            public readonly FileTable.CachingBuilder FileTableBuilder;
            public readonly WorkerTable.CachingBuilder WorkerTableBuilder;

            public Builder(PackedPipGraph pipGraph)
            {
                PipGraph = pipGraph;
                StringTableBuilder = new StringTable.CachingBuilder(PipGraph.StringTable);
                PipTableBuilder = new PipTable.Builder(PipGraph.PipTable, StringTableBuilder);
                FileTableBuilder = new FileTable.CachingBuilder(PipGraph.FileTable, StringTableBuilder);
                WorkerTableBuilder = new WorkerTable.CachingBuilder(PipGraph.WorkerTable, StringTableBuilder);
            }
        }
    }
}
