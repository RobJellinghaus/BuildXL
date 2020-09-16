// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using BuildXL.Execution.Analyzers.PackedExecution;
using BuildXL.Execution.Analyzers.PackedTable;
using BuildXL.Pips;
using BuildXL.Pips.Graph;
using BuildXL.Pips.Operations;
using BuildXL.Scheduler;
using BuildXL.Scheduler.Fingerprints;
using BuildXL.Scheduler.Tracing;
using BuildXL.ToolSupport;
using BuildXL.Utilities;
using BuildXL.Utilities.ParallelAlgorithms;
using Microsoft.VisualStudio.Services.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.ContractsLight;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BuildXL.Execution.Analyzer
{
    // The PackedExecution has a quite different implementation of the very similar concepts,
    // so in this analyzer we refer to them separately.
    // We use the ugly but unmissable convention of "B_" meaning "BuildXL" and "P_" meaning "PackedExecution".

    using B_PipId = Pips.PipId;
    using B_PipType = BuildXL.Pips.Operations.PipType;

    using P_PipId = BuildXL.Execution.Analyzers.PackedExecution.PipId;
    using P_PipTable = BuildXL.Execution.Analyzers.PackedExecution.PipTable;
    using P_PipType = BuildXL.Execution.Analyzers.PackedExecution.PipType;

    internal partial class Args
    {
        // Required flags
        private const string OutputDirectoryOption = "OutputDirectory";

        // Optional flags
        public Analyzer InitializePackedExecutionExporter()
        {
            string outputDirectoryPath = null;

            foreach (Option opt in AnalyzerOptions)
            {
                if (opt.Name.Equals(OutputDirectoryOption, StringComparison.OrdinalIgnoreCase))
                {
                    outputDirectoryPath = ParseSingletonPathOption(opt, outputDirectoryPath);
                }
                else
                {
                    throw Error("Unknown option for PackedExecutionExporter: {0}", opt.Name);
                }
            }

            if (string.IsNullOrEmpty(outputDirectoryPath))
            {
                throw Error("/outputDirectory parameter is required");
            }

            return new PackedExecutionExporter(GetAnalysisInput(), outputDirectoryPath);
        }

        private static void WritePackedExecutionExporterHelp(HelpWriter writer)
        {
            writer.WriteBanner("Packed Execution Exporter");
            writer.WriteModeOption(nameof(AnalysisMode.PackedExecutionExporter), "Exports the pip graph and execution data in PackedExecution format");
            writer.WriteLine("Required");
            writer.WriteOption(OutputDirectoryOption, "The location of the output directory.");
        }
    }

    /// <summary>
    /// Exports the build graph and execution data in PackedExecution format.
    /// </summary>
    /// <remarks>
    /// The PackedExecution format arranges data in large tables of unmanaged structs,
    /// enabling very fast saving and loading, and providing convenient pre-indexed
    /// relational structure.
    /// </remarks>
    public sealed class PackedExecutionExporter : Analyzer
    {
        private readonly string m_outputDirectoryPath;

        /// <summary>
        /// The PackedExecution instance being built.
        /// </summary>
        private readonly PackedExecution m_packedExecution;

        /// <summary>
        /// The Builder for the PackedExecution being built.
        /// </summary>
        private readonly PackedExecution.Builder m_packedExecutionBuilder;

        /// <summary>
        /// Side dictionary tracking the BuildXL FileArtifact objects, for use when finding the producing pip.
        /// </summary>
        private readonly Dictionary<AbsolutePath, (FileId fileId, FileArtifact fileArtifact)> m_pathsToFiles = 
            new Dictionary<AbsolutePath, (FileId fileId, FileArtifact fileArtifact)>();

        /// <summary>
        /// Side dictionary tracking the BuildXL DictionaryArtifact objects, for use when finding the producing pip.
        /// </summary>
        private readonly Dictionary<AbsolutePath, (DirectoryId directoryId, DirectoryArtifact directoryArtifact)> m_pathsToDirectories =
            new Dictionary<AbsolutePath, (DirectoryId directoryId, DirectoryArtifact directoryArtifact)>();

        /// <summary>
        /// List of paths to decided files (either materialized from cache, or produced).
        /// </summary>
        private readonly HashSet<AbsolutePath> m_decidedFiles = new HashSet<AbsolutePath>();

        /// <summary>
        /// Upwards index from files to their containing director(ies).
        /// </summary>
        private readonly Dictionary<AbsolutePath, List<DirectoryArtifact>> m_parentOutputDirectory = 
            new Dictionary<AbsolutePath, List<DirectoryArtifact>>();

        /// <summary>
        /// Count of processed pips.
        /// </summary>
        private int m_processedPipCount;

        /// <summary>
        /// Count of processed files.
        /// </summary>
        private int m_processedFileCount;

        /// <summary>
        /// The list of WorkerAnalyzer instances which consume per-worker data.
        /// </summary>
        private readonly List<WorkerAnalyzer> m_workerAnalyzers = new List<WorkerAnalyzer>();

        public PackedExecutionExporter(AnalysisInput input, string outputDirectoryPath)
            : base(input)
        {
            m_outputDirectoryPath = outputDirectoryPath;

            m_packedExecution = new PackedExecution();
            // we'll be building these right away, not loading the tables first
            m_packedExecution.ConstructRelationTables();

            m_packedExecutionBuilder = new PackedExecution.Builder(m_packedExecution);

            Console.WriteLine($"PackedExecutionExporter: Constructed at {DateTime.Now}.");
        }

        #region Analyzer event handlers

        public override void WorkerList(WorkerListEventData data)
        {
            foreach (string workerName in data.Workers)
            {
                WorkerId workerId = m_packedExecutionBuilder.WorkerTableBuilder.GetOrAdd(workerName);
                m_workerAnalyzers.Add(new WorkerAnalyzer(this, workerName, workerId));
            }
        }

        /// <summary>
        /// File artifact content (size, origin) is now known; create a FileTable entry for this file.
        /// </summary>
        public override void FileArtifactContentDecided(FileArtifactContentDecidedEventData data)
        {
            if (data.FileArtifact.IsOutputFile && data.FileContentInfo.HasKnownLength)
            {
                if (((++m_processedFileCount) % 1000000) == 0)
                {
                    Console.WriteLine($"Processed {m_processedFileCount} files...");
                }

                ContentFlags contentFlags = default;
                switch (data.OutputOrigin)
                {
                    case Scheduler.PipOutputOrigin.DeployedFromCache:
                        contentFlags = ContentFlags.MaterializedFromCache;
                        break;
                    case Scheduler.PipOutputOrigin.Produced:
                        contentFlags = ContentFlags.Produced;
                        break;
                        // We ignore the following cases:
                        // PipOutputOrigin.NotMaterialized - we cannot infer the current or future materialization status of a file
                        // PipOutputOrigin.UpToDate - not relevant to this analyzer
                }

                // TODO: evaluate optimizing this with a direct hierarchical BXL-Path-to-PackedTable-Name mapping
                string pathString = data.FileArtifact.Path.ToString(PathTable).ToCanonicalizedPath();
                FileId fileId = m_packedExecutionBuilder.FileTableBuilder.GetOrAdd(
                    pathString,
                    data.FileContentInfo.Length, 
                    default,
                    contentFlags);

                // And save the FileId and the FileArtifact for later use when searching for producing pips.
                m_pathsToFiles[data.FileArtifact.Path] = (fileId, data.FileArtifact);

                m_decidedFiles.Add(data.FileArtifact.Path);
            }
        }

        /// <summary>
        /// A process fingerprint was computed; use the execution data.
        /// </summary>
        public override void ProcessFingerprintComputed(ProcessFingerprintComputationEventData data)
        {
            if (data.Kind == FingerprintComputationKind.Execution)
            {
                if (((++m_processedPipCount) % 1000) == 0)
                {
                    Console.WriteLine($"Processed {m_processedPipCount} pips...");
                }
            }

            GetWorkerAnalyzer().ProcessFingerprintComputed(data);
        }

        /// <summary>
        /// The directory outputs of a pip are now known; index the directory contents
        /// </summary>
        /// <param name="data"></param>
        public override void PipExecutionDirectoryOutputs(PipExecutionDirectoryOutputs data)
        {
            foreach (var kvp in data.DirectoryOutputs)
            {
                DirectoryId directoryId = m_packedExecutionBuilder.DirectoryTableBuilder.GetOrAdd(
                    kvp.directoryArtifact.Path.ToString(PathTable).ToCanonicalizedPath(),
                    default,
                    default);

                m_pathsToDirectories[kvp.directoryArtifact.Path] = (directoryId, kvp.directoryArtifact);

                foreach (FileArtifact fileArtifact in kvp.fileArtifactArray)
                {
                    // BXL: Is there an invariant that FileArtifactContentDecided will be called for a given file
                    // before PipExecutionDirectoryOutputs will be called for the directory containing that file?
                    // (If so, this can just be a lookup in m_pathsToFiles, instead of a GetOrAdd for a possibly new file.)
                    FileId fileId = m_packedExecutionBuilder.FileTableBuilder.GetOrAdd(
                        fileArtifact.Path.ToString(PathTable).ToCanonicalizedPath(),
                        default, default, default);

                    m_pathsToFiles[fileArtifact.Path] = (fileId, fileArtifact);

                    m_packedExecutionBuilder.DirectoryContentsBuilder.Add(directoryId, fileId);

                    // make the index from files up to containing directories
                    // TODO: when can there be more than one entry in this list?
                    if (!m_parentOutputDirectory.TryGetValue(fileArtifact.Path, out List<DirectoryArtifact> parents))
                    {
                        parents = new List<DirectoryArtifact>();
                        m_parentOutputDirectory.Add(fileArtifact.Path, parents);
                    }
                    parents.Add(kvp.directoryArtifact);
                }
            }
        }

        #endregion

        /// <summary>
        /// Get the WorkerAnalyzer instance for the current worker ID (available as a base field on the analyzer).
        /// </summary>
        /// <returns></returns>
        private WorkerAnalyzer GetWorkerAnalyzer()
        {
            return m_workerAnalyzers[(int)CurrentEventWorkerId];
        }

        public override int Analyze()
        {
            Console.WriteLine($"PackedExecutionExporter: Starting export at {DateTime.Now}.");

            if (!Directory.Exists(m_outputDirectoryPath))
            {
                Directory.CreateDirectory(m_outputDirectoryPath);
            }

            var pipList = BuildPips();

            BuildPipDependencies(pipList);

            BuildProcessPipRelations();

            BuildFileProducers();

            // and write it out
            m_packedExecution.SaveToDirectory(m_outputDirectoryPath);

            Console.WriteLine($"PackedExecutionExporter: Wrote out packed execution at {DateTime.Now}.");

            return 0;
        }

        #region Pips

        private List<PipReference> BuildPips()
        {
            List<PipReference> pipList =
                PipGraph.AsPipReferences(PipTable.StableKeys, PipQueryContext.PipGraphRetrieveAllPips).ToList();

            // Populate the PipTable with all known PipIds, defining a mapping from the BXL PipIds to the PackedExecution PipIds.
            for (int i = 0; i < pipList.Count; i++)
            {
                // Each pip gets a P_PipId that is one plus its index in this list, since zero is never a PackedExecution ID value.
                // This seems to be exactly how B_PipId works as well, but we verify to be sure we can rely on this invariant.
                P_PipId graphPipId = AddPip(pipList[i], m_packedExecutionBuilder.PipTableBuilder);
                if (graphPipId.Value != pipList[i].PipId.Value)
                {
                    throw new ArgumentException($"Graph pip ID {graphPipId.Value} does not equal BXL pip ID {pipList[i].PipId.Value}");
                }
            }

            Console.WriteLine($"PackedExecutionExporter: Added {PipGraph.PipCount} pips at {DateTime.Now}.");



            return pipList;
        }

        /// <summary>
        /// Add this pip's informationn to the graph.
        /// </summary>
        public P_PipId AddPip(PipReference pipReference, P_PipTable.Builder pipBuilder)
        {
            Pip pip = pipReference.HydratePip();
            string pipName = GetDescription(pip).Replace(", ", ".");
            // strip the pip hash from the start of the description
            if (pipName.StartsWith("Pip"))
            {
                int firstDotIndex = pipName.IndexOf('.');
                if (firstDotIndex != -1)
                {
                    pipName = pipName.Substring(firstDotIndex + 1);
                }
            }
            P_PipType pipType = (P_PipType)(int)pip.PipType;

            P_PipId g_pipId = pipBuilder.Add(pip.SemiStableHash, pipName, pipType);

            return g_pipId;
        }

        private void BuildPipDependencies(List<PipReference> pipList)
        {
            // and now do it again with the dependencies, now that everything is established.
            // Since we added all the pips in pipList order to PipTable, we can traverse them again in the same order
            // to build the relation.
            SpannableList<P_PipId> buffer = new SpannableList<P_PipId>(); // to accumulate the IDs we add to the relation
            for (int i = 0; i < pipList.Count; i++)
            {
                IEnumerable<P_PipId> pipDependencies = PipGraph
                    .RetrievePipReferenceImmediateDependencies(pipList[i].PipId, null)
                    .Where(pipRef => pipRef.PipType != B_PipType.HashSourceFile)
                    .Select(pid => pid.PipId.Value)
                    .Distinct()
                    .OrderBy(pid => pid)
                    .Select(pid => new P_PipId((int)pid));

                // reuse the buffer for span purposes
                buffer.Clear();
                buffer.AddRange(pipDependencies);

                // don't need to use a builder here, we're adding all dependencies in PipId order
                m_packedExecution.PipDependencies.Add(buffer.AsSpan());
            }

            Console.WriteLine($"PackedExecutionExporter: Added {m_packedExecution.PipDependencies.MultiValueCount} total dependencies at {DateTime.Now}.");
        }

        private void BuildProcessPipRelations()
        {
            // BXL: isn't it necessary to Complete() all WorkerAnalyzers before starting to iterate over their process pip information?
            // How else can that result be synchronized?
            int totalProcessPips = 0;
            foreach (var worker in m_workerAnalyzers)
            {
                Console.WriteLine($"Completing worker {worker.Name}");
                worker.Complete();

                // TODO: determine whether these m_pathsToWherever lookups are doomed -- do we have to create new files/directories here?
                foreach (WorkerAnalyzer.ProcessPipInfo processPipInfo in worker.ProcessPipInfoList)
                {
                    foreach (AbsolutePath declaredInputFilePath in processPipInfo.DeclaredInputFiles)
                    {
                        m_packedExecutionBuilder.DeclaredInputFilesBuilder.Add(processPipInfo.PipId, m_pathsToFiles[declaredInputFilePath].fileId);
                    }

                    foreach (DirectoryArtifact directoryArtifact in processPipInfo.DeclaredInputDirectories)
                    {
                        m_packedExecutionBuilder.DeclaredInputDirectoriesBuilder.Add(processPipInfo.PipId, m_pathsToDirectories[directoryArtifact.Path].directoryId);
                    }

                    foreach (AbsolutePath consumedInputPath in processPipInfo.ConsumedFiles)
                    {
                        m_packedExecutionBuilder.ConsumedFilesBuilder.Add(processPipInfo.PipId, m_pathsToFiles[consumedInputPath].fileId);
                    }

                    totalProcessPips++;
                }
            }

            m_packedExecutionBuilder.DeclaredInputFilesBuilder.Complete();
            m_packedExecutionBuilder.DeclaredInputDirectoriesBuilder.Complete();
            m_packedExecutionBuilder.ConsumedFilesBuilder.Complete();

            Console.WriteLine($"PackedExecutionExporter: Analyzed {totalProcessPips} executed process pips at {DateTime.Now}.");
        }

        private void BuildFileProducers()
        {
            // set file producer
            Parallel.ForEach(
                m_decidedFiles,
                new ParallelOptions() { MaxDegreeOfParallelism = 1 }, // B4PR: Environment.ProcessorCount },
                path =>
                {
                    var tuple = m_pathsToFiles[path];

                    // try to find a producer of this file
                    var producer = PipGraph.TryGetProducer(tuple.fileArtifact);
                    if (!producer.IsValid)
                    {
                        // it's not a statically declared file, so it must be produced as a part of an output directory
                        if (m_parentOutputDirectory.TryGetValue(path, out var containingDirectories))
                        {
                            foreach (var directory in containingDirectories)
                            {
                                producer = PipGraph.TryGetProducer(directory);
                                if (producer.IsValid)
                                {
                                    break;
                                }
                            }
                        }
                    }

                    if (producer.IsValid)
                    {
                        // Theoretically this is safe if we never update the same file twice with different producers.
                        // BXL: should we have an interlock to guard against this?
                        // BXL: can a file have multiple producers legitimately? (PipGraph has some kind of method for getting all producers of a file)
                        FileEntry entry = m_packedExecution.FileTable[tuple.fileId];

                        Contract.Assert(entry.ProducerPip.Equals(default(P_PipId)),
                            $"Should not have set producer pip {entry.ProducerPip} yet for file {m_packedExecution.PathTable.GetText(entry.Path)}");

                        m_packedExecution.FileTable[tuple.fileId] = entry.WithProducerPip(new P_PipId((int)producer.Value));
                    }
                });

            Console.WriteLine($"PackedExecutionExporter: Analyzed {m_decidedFiles.Count} decided files (to determine their producers) at {DateTime.Now}.");
        }

        #endregion

        #region WorkerAnalyzer

        /// <summary>
        /// Implement some asynchronous, thread-pooled processing of incoming data about processed pips.
        /// </summary>
        /// <remarks>
        /// Finding the producer pip for a file is evidently expensive and shouldn't be done on the execution log event thread.
        /// </remarks>
        private class WorkerAnalyzer
        {
            /// <summary>
            /// The information about an executed process pip.
            /// </summary>
            /// <remarks>
            /// TODO: Consider making this less object-y and more columnar... store all the declared input files
            /// in just one big list indexed by PipId, etc.
            /// </remarks>
            public struct ProcessPipInfo
            {
                public readonly P_PipId PipId;
                public readonly List<AbsolutePath> DeclaredInputFiles;
                public readonly List<DirectoryArtifact> DeclaredInputDirectories;
                public readonly List<AbsolutePath> ConsumedFiles;
                public readonly WorkerId Worker;

                /// <summary>
                /// Hacky constructor to initialize collections
                /// </summary>
                /// <param name="_"></param>
                public ProcessPipInfo(
                    P_PipId pipId, 
                    List<AbsolutePath> declaredInputFiles, 
                    List<DirectoryArtifact> declaredInputDirs, 
                    List<AbsolutePath> consumedFiles, 
                    WorkerId worker)
                {
                    PipId = pipId;
                    DeclaredInputFiles = declaredInputFiles;
                    DeclaredInputDirectories = declaredInputDirs;
                    ConsumedFiles = consumedFiles;
                    Worker = worker;
                }
            }

            /// <summary>
            /// ID of this analyzer's worker.
            /// </summary>
            private readonly WorkerId m_workerId;

            /// <summary>
            /// The parent exporter.
            /// </summary>
            private readonly PackedExecutionExporter m_exporter;

            /// <summary>
            /// This analyzer's concurrency manager.
            /// </summary>
            private readonly ActionBlockSlim<ProcessFingerprintComputationEventData> m_processingBlock;

            /// <summary>
            /// Each WorkerAnalyzer collects its own partial relations and merges them when complete.
            /// </summary>
            public readonly List<ProcessPipInfo> ProcessPipInfoList = new List<ProcessPipInfo>();

            public string Name { get; }

            public WorkerAnalyzer(PackedExecutionExporter exporter, string name, WorkerId workerId)
            {
                m_exporter = exporter;
                Name = name;
                m_workerId = workerId;

                m_processingBlock = new ActionBlockSlim<ProcessFingerprintComputationEventData>(1, ProcessFingerprintComputedCore);
            }

            public void Complete()
            {
                m_processingBlock.Complete();
            }

            public void ProcessFingerprintComputed(ProcessFingerprintComputationEventData data)
            {
                m_processingBlock.Post(data);
            }

            public void ProcessFingerprintComputedCore(ProcessFingerprintComputationEventData data)
            {
                var pip = m_exporter.GetPip(data.PipId) as Process;
                Contract.Assert(pip != null);

                // only interested in the events generated after a corresponding pip was executed
                // however, we still need to save pip description so there would be no missing entries in pips.csv
                if (data.Kind != FingerprintComputationKind.Execution)
                {
                    return;
                }

                // part 1: collect requested inputs
                // count only output files/directories
                var declaredInputFiles = pip.Dependencies.Where(f => f.IsOutputFile).Select(f => f.Path).ToList();
                var declaredInputDirs = pip.DirectoryDependencies.Where(d => d.IsOutputDirectory()).ToList();

                var packedExecution = m_exporter.m_packedExecution;
                var fileTable = packedExecution.FileTable;
                var pathsToFiles = m_exporter.m_pathsToFiles;

                // part 2: collect actual inputs                
                var consumedPaths = data.StrongFingerprintComputations.Count == 0
                    ? new List<AbsolutePath>()
                    : data.StrongFingerprintComputations[0].ObservedInputs
                        .Where(input => input.Type == ObservedInputType.FileContentRead || input.Type == ObservedInputType.ExistingFileProbe)
                        .Select(input => input.Path)
                        .Where(path => pathsToFiles.TryGetValue(path, out var tuple) && fileTable[tuple.fileId].SizeInBytes > 0)
                        .ToList();

                P_PipId packedPipId = new P_PipId((int)data.PipId.Value);
                ProcessPipInfoList.Add(new ProcessPipInfo(packedPipId, declaredInputFiles, declaredInputDirs, consumedPaths, m_workerId));
            }

            #endregion
        }
    }
}
