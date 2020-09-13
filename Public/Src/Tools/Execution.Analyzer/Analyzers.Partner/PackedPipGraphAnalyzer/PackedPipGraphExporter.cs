// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BuildXL.Execution.Analyzer.Analyzers;
using BuildXL.Execution.Analyzers.PackedPipGraph;
using BuildXL.Pips;
using BuildXL.Pips.Operations;
using BuildXL.ToolSupport;

namespace BuildXL.Execution.Analyzer
{
    // The PackedPipGraph has a quite different implementation of the very similar concepts,
    // so in this analyzer we refer to them separately.
    // We use the ugly but unmissable convention of "G_" meaning "PackedPipGraph" and "B_" meaning "BuildXL".

    using B_PipId = BuildXL.Pips.PipId;
    using B_PipType = BuildXL.Pips.Operations.PipType;

    using G_PipId = BuildXL.Execution.Analyzers.PackedPipGraph.PipId;
    using G_PipTable = BuildXL.Execution.Analyzers.PackedPipGraph.PipTable;
    using G_PipType = BuildXL.Execution.Analyzers.PackedPipGraph.PipType;
    using G_StringTable = BuildXL.Execution.Analyzers.PackedPipGraph.StringTable;

    internal partial class Args
    {
        // Required flags
        private const string OutputDirectoryOption = "OutputDirectory";

        // Optional flags
        public Analyzer InitializePackedPipGraphExporter()
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
                    throw Error("Unknown option for PackedPipGraphExporter: {0}", opt.Name);
                }
            }

            if (string.IsNullOrEmpty(outputDirectoryPath))
            {
                throw Error("/outputDirectory parameter is required");
            }

            return new PackedPipGraphExporter(GetAnalysisInput(), outputDirectoryPath);
        }

        private static void WritePackedPipGraphExporterHelp(HelpWriter writer)
        {
            writer.WriteBanner("Packed Pip Graph Exporter");
            writer.WriteModeOption(nameof(AnalysisMode.PackedPipGraphExporter), "Exports the pip graph and execution data in PackedPipGraph form");
            writer.WriteLine("Required");
            writer.WriteOption(OutputDirectoryOption, "The location of the output directory.");
        }
    }

    /// <summary>
    /// Exports the build graph data in PackedPipGraph format.
    /// </summary>
    /// <remarks>
    /// PackedPipGraph is a small experiment in using Span[T] and unmanaged to represent BXL data
    /// with minimal overhead.
    /// </remarks>
    public sealed class PackedPipGraphExporter : Analyzer
    {
        private readonly string m_outputDirectoryPath;

        public PackedPipGraphExporter(AnalysisInput input, string outputDirectoryPath)
            : base(input)
        {
            m_outputDirectoryPath = outputDirectoryPath;

            Console.WriteLine($"PackedPipGraphExporter: Constructed at {DateTime.Now}.");
        }

        public override int Analyze()
        {
            Console.WriteLine($"PackedPipGraphExporter: Starting export at {DateTime.Now}.");

            if (!Directory.Exists(m_outputDirectoryPath))
            {
                Directory.CreateDirectory(m_outputDirectoryPath);
            }

            PackedPipGraph pipGraph = new PackedPipGraph();
            G_StringTable.CachingBuilder stringBuilder = new G_StringTable.CachingBuilder(pipGraph.StringTable);
            G_PipTable.Builder pipBuilder = new G_PipTable.Builder(pipGraph.PipTable, stringBuilder);

            List<PipReference> pipList =
                PipGraph.AsPipReferences(PipTable.StableKeys, PipQueryContext.PipGraphRetrieveAllPips).ToList();

            // Populate the PipTable with all known PipIds, defining a mapping from the BXL PipIds to the PackedPipGraph PipIds.
            for (int i = 0; i < pipList.Count; i++)
            {
                // Each pip gets a G_PipId that is one plus its index in this list, since zero is never a PackedPipGraph ID value.
                // This seems to be exactly how B_PipId works as well, but we verify to be sure we can rely on this invariant.
                G_PipId graphPipId = AddPip(pipList[i], pipBuilder);
                if (graphPipId.Value != pipList[i].PipId.Value)
                {
                    throw new ArgumentException($"Graph pip ID {graphPipId.Value} does not equal BXL pip ID {pipList[i].PipId.Value}");
                }
            }

            Console.WriteLine($"PackedPipGraphExporter: Added {PipGraph.PipCount} pips at {DateTime.Now}.");

            // Now that all pips are loaded, construct the PipDependencies RelationTable.
            pipGraph.ConstructRelationTables();

            // and now do it again with the dependencies, now that everything is established.
            // Since we added all the pips in pipList order to PipTable, we can traverse them again in the same order
            // to build the relation.
            SpannableList<G_PipId> buffer = new SpannableList<G_PipId>(); // to accumulate the IDs we add to the relation
            for (int i = 0; i < pipList.Count; i++)
            {
                AddPipDependencies(
                    new G_PipId(i + 1),
                    pipList[i],
                    pipGraph.PipDependencies,
                    buffer);
            }

            Console.WriteLine($"PackedPipGraphExporter: Added {pipGraph.PipDependencies.MultiValueCount} total dependencies at {DateTime.Now}.");

            // and write it out
            pipGraph.SaveToDirectory(m_outputDirectoryPath);

            Console.WriteLine($"PackedPipGraphExporter: Wrote out pip graph at {DateTime.Now}.");

            return 0;
        }

        /// <summary>
        /// Add this pip's informationn to the graph.
        /// </summary>
        public G_PipId AddPip(PipReference pipReference, G_PipTable.Builder pipBuilder)
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
            G_PipType pipType = (G_PipType)(int)pip.PipType;

            G_PipId g_pipId = pipBuilder.Add(pip.SemiStableHash, pipName, pipType);

            return g_pipId;

            /* not relevant (yet?)
            PipProvenance provenance = pip.Provenance;
            pipMetadata.Qualifier = PipGraph.Context.QualifierTable.GetCanonicalDisplayString(provenance.QualifierId);
            pipMetadata.Usage = provenance.Usage.IsValid ? provenance.Usage.ToString(PathTable) : null;
            pipMetadata.SpecFilePath = provenance.Token.Path.ToString(PathTable);
            pipMetadata.OutputValueSymbol = provenance.OutputValueSymbol.ToString(SymbolTable);
            pipMetadata.ModuleId = provenance.ModuleId.Value.Value;
            pipMetadata.SpecFilePath = provenance.Token.Path.ToString(PathTable);
            */
        }

        public void AddPipDependencies(
            G_PipId g_pipId, 
            PipReference pipReference,
            RelationTable<G_PipId, G_PipId> relationTable,
            SpannableList<G_PipId> buffer)
        {
            IEnumerable<G_PipId> pipDependencies = PipGraph
                .RetrievePipReferenceImmediateDependencies(pipReference.PipId, null)
                .Where(pipRef => pipRef.PipType != B_PipType.HashSourceFile)
                .Select(pid => pid.PipId.Value)
                .Distinct()
                .OrderBy(pid => pid)
                .Select(pid => new G_PipId((int)pid));

            buffer.Clear();
            buffer.AddRange(pipDependencies);
            relationTable.Add(buffer.AsSpan());
        }

        // These are all copied from CosineDumpPip and are not yet used by this analyzer; as we extend PackedPipGraph to capture more of this,
        // we'll use this code as a guide.
        #region NotYetUsed_PipDetailsGenerators

        ///
        /// Notes
        /// * To keep from serializing the empty enumerables that LINQ can create, they must be manually made null if the list is empty
        ///

        /// <summary>
        /// Generates the CopyFilePipDetails for a given Pip
        /// </summary>
        public CopyFilePipDetails GenerateCopyFilePipDetails(CopyFile pip)
        {
            CopyFilePipDetails copyFilePipDetails = new CopyFilePipDetails
            {
                Source = pip.Source.IsValid ? pip.Source.Path.ToString(PathTable) : null,
                Destination = pip.Destination.IsValid ? pip.Destination.Path.ToString(PathTable) : null
            };

            return copyFilePipDetails;
        }

        /// <summary>
        /// Generates the ProcessPipDetails for a given Pip
        /// </summary>
        public ProcessPipDetails GenerateProcessPipDetails(Process pip)
        {
            ProcessPipDetails processPipDetails = new ProcessPipDetails();

            InvocationDetails invoDetails = new InvocationDetails
            {
                Executable = pip.Executable.IsValid ? pip.Executable.Path.ToString(PathTable): null,
                ToolDescription = pip.ToolDescription.IsValid ? pip.ToolDescription.ToString(StringTable) : null,
                ResponseFilePath = pip.ResponseFile.IsValid ? pip.ResponseFile.Path.ToString(PathTable) : null,
                Arguments = pip.Arguments.IsValid ? pip.Arguments.ToString(PathTable) : null,
                ResponseFileContents = pip.ResponseFileData.IsValid ? pip.ResponseFileData.ToString(PathTable) : null,
            };
            invoDetails.EnvironmentVariables = pip.EnvironmentVariables.
                                                Select(x => new KeyValuePair<string, string>
                                                (x.Name.ToString(StringTable), x.Value.IsValid ? x.Value.ToString(PathTable) : null))
                                                .ToList();
            invoDetails.EnvironmentVariables = invoDetails.EnvironmentVariables.Any() ? invoDetails.EnvironmentVariables : null;
            processPipDetails.InvocationDetails = invoDetails;

            InputOutputDetails inOutDetails = new InputOutputDetails
            {
                STDInFile = pip.StandardInput.File.IsValid ? pip.StandardInput.File.Path.ToString(PathTable): null,
                STDOut = pip.StandardOutput.IsValid ? pip.StandardOutput.Path.ToString(PathTable): null,
                STDError = pip.StandardError.IsValid ? pip.StandardError.Path.ToString() : null,
                STDDirectory = pip.StandardDirectory.IsValid ? pip.StandardDirectory.ToString(PathTable) : null,
                WarningRegex = pip.WarningRegex.IsValid ? pip.WarningRegex.Pattern.ToString(StringTable) : null,
                ErrorRegex = pip.ErrorRegex.IsValid ? pip.ErrorRegex.Pattern.ToString(StringTable) : null,
                STDInData = pip.StandardInputData.IsValid ? pip.StandardInputData.ToString(PathTable) : null
            };

            processPipDetails.InputOutputDetails = inOutDetails;

            DirectoryDetails dirDetails = new DirectoryDetails
            {
                WorkingDirectory = pip.WorkingDirectory.IsValid ? pip.WorkingDirectory.ToString(PathTable) : null,
                UniqueOutputDirectory = pip.UniqueOutputDirectory.IsValid ? pip.UniqueOutputDirectory.ToString(PathTable) : null,
                TempDirectory = pip.TempDirectory.IsValid ? pip.TempDirectory.ToString(PathTable) : null,
            };
            if(pip.AdditionalTempDirectories.IsValid)
            {
                dirDetails.ExtraTempDirectories = pip.AdditionalTempDirectories.
                                                    Select(x => x.ToString(PathTable))
                                                    .ToList();
            }
            dirDetails.ExtraTempDirectories = dirDetails.ExtraTempDirectories.Any() ? dirDetails.ExtraTempDirectories : null;
            processPipDetails.DirectoryDetails = dirDetails;

            AdvancedOptions advancedOptions = new AdvancedOptions
            {
                TimeoutWarning = pip.WarningTimeout.GetValueOrDefault(),
                TimeoutError = pip.Timeout.GetValueOrDefault(),
                SuccessCodes = pip.SuccessExitCodes.ToList(),
                Semaphores =  pip.Semaphores.Select(x => x.Name.ToString(StringTable)).ToList(),
                HasUntrackedChildProcesses = pip.HasUntrackedChildProcesses,
                ProducesPathIndependentOutputs = pip.ProducesPathIndependentOutputs,
                OutputsMustRemainWritable = pip.OutputsMustRemainWritable,
                AllowPreserveOutputs = pip.AllowPreserveOutputs
            };
            advancedOptions.SuccessCodes = advancedOptions.SuccessCodes.Any() ? advancedOptions.SuccessCodes : null;
            advancedOptions.Semaphores = advancedOptions.Semaphores.Any() ? advancedOptions.Semaphores : null;
            processPipDetails.AdvancedOptions = advancedOptions;

            ProcessInputOutputDetails procInOutDetails = new ProcessInputOutputDetails
            {
                FileDependencies = pip.Dependencies.Select(x => x.IsValid ? x.Path.ToString(PathTable) : null).ToList(),
                DirectoryDependencies = pip.DirectoryDependencies.Select(x => x.IsValid ? x.Path.ToString(PathTable) : null).ToList(),
                OrderDependencies = pip.OrderDependencies.Select(x => x.Value).ToList(),
                FileOutputs = pip.FileOutputs.Select(x => x.IsValid ? x.Path.ToString(PathTable) : null).ToList(),
                DirectoryOuputs = pip.DirectoryOutputs.Select(x => x.IsValid ? x.Path.ToString(PathTable) : null).ToList(),
                UntrackedPaths = pip.UntrackedPaths.Select(x => x.IsValid ? x.ToString(PathTable) : null).ToList(),
                UntrackedScopes = pip.UntrackedScopes.Select(x => x.IsValid ? x.ToString(PathTable) : null).ToList(),
            };
            procInOutDetails.FileDependencies = procInOutDetails.FileDependencies.Any() ? procInOutDetails.FileDependencies : null;
            procInOutDetails.DirectoryDependencies = procInOutDetails.DirectoryDependencies.Any() ? procInOutDetails.DirectoryDependencies : null;
            procInOutDetails.OrderDependencies = procInOutDetails.OrderDependencies.Any() ? procInOutDetails.OrderDependencies : null;
            procInOutDetails.FileOutputs = procInOutDetails.FileOutputs.Any() ? procInOutDetails.FileOutputs : null;
            procInOutDetails.DirectoryOuputs = procInOutDetails.DirectoryOuputs.Any() ? procInOutDetails.DirectoryOuputs : null;
            procInOutDetails.UntrackedPaths = procInOutDetails.UntrackedPaths.Any() ? procInOutDetails.UntrackedPaths : null;
            procInOutDetails.UntrackedScopes = procInOutDetails.UntrackedScopes.Any() ? procInOutDetails.UntrackedScopes : null;
            processPipDetails.ProcessInputOutputDetails = procInOutDetails;

            ServiceDetails servDetails = new ServiceDetails
            {
                IsService = pip.IsService,
                ShutdownProcessPipId = pip.ShutdownProcessPipId.Value,
                ServicePipDependencies = pip.ServicePipDependencies.Select(x => x.Value).ToList(),
                IsStartOrShutdownKind = pip.IsStartOrShutdownKind
            };
            servDetails.ServicePipDependencies = servDetails.ServicePipDependencies.Any() ? servDetails.ServicePipDependencies : null;
            processPipDetails.ServiceDetails = servDetails;

            return processPipDetails;
        }

        /// <summary>
        /// Generates the IpcPipDetails for a given Pip
        /// </summary>
        public IpcPipDetails GenerateIpcPipDetails(IpcPip pip)
        {
            IpcPipDetails ipcPipDetails = new IpcPipDetails
            {
                IpcMonikerId = pip.IpcInfo.IpcMonikerId.Value,
                MessageBody = pip.MessageBody.IsValid ? pip.MessageBody.ToString(PathTable) : null,
                OutputFile = pip.OutputFile.Path.ToString(PathTable),
                ServicePipDependencies = pip.ServicePipDependencies.Select(x => x.Value).ToList(),
                FileDependencies = pip.FileDependencies.Select(x => x.Path.ToString(PathTable)).ToList(),
                LazilyMaterializedDependencies = pip.LazilyMaterializedDependencies.Select(x => x.Path.ToString(PathTable)).ToList(),
                IsServiceFinalization = pip.IsServiceFinalization,
                MustRunOnMaster = pip.MustRunOnMaster
            };
            ipcPipDetails.ServicePipDependencies = ipcPipDetails.ServicePipDependencies.Any() ? ipcPipDetails.ServicePipDependencies : null;
            ipcPipDetails.FileDependencies = ipcPipDetails.FileDependencies.Any() ? ipcPipDetails.FileDependencies : null;
            ipcPipDetails.LazilyMaterializedDependencies = ipcPipDetails.LazilyMaterializedDependencies.Any() ? ipcPipDetails.LazilyMaterializedDependencies : null;

            return ipcPipDetails;
        }

        /// <summary>
        /// Generates the ValuePipDetails for a given Pip
        /// </summary>
        public ValuePipDetails GenerateValuePipDetails(ValuePip pip)
        {
            ValuePipDetails valuePipDetails = new ValuePipDetails
            {
                Symbol = pip.Symbol.ToString(SymbolTable),
                Qualifier = pip.Qualifier.GetHashCode(),
                SpecFilePath = pip.LocationData.Path.ToString(PathTable),
                Location = pip.LocationData.ToString(PathTable)
            };

            return valuePipDetails;
        }

        /// <summary>
        /// Generates the SpecFilePipDetails for a given Pip
        /// </summary>
        public SpecFilePipDetails GenerateSpecFilePipDetails(SpecFilePip pip)
        {
            SpecFilePipDetails specFilePipDetails = new SpecFilePipDetails
            {
                SpecFile = pip.SpecFile.Path.ToString(PathTable),
                DefinitionFilePath = pip.DefinitionLocation.Path.ToString(PathTable),
                Location = pip.DefinitionLocation.ToString(PathTable),
                ModuleId = pip.OwningModule.Value.Value
            };

            return specFilePipDetails;
        }

        /// <summary>
        /// Generates the ModulePipDetails for a given Pip
        /// </summary>
        public ModulePipDetails GenerateModulePipDetails(ModulePip pip)
        {
            ModulePipDetails modulePipDetails = new ModulePipDetails
            {
                Identity = pip.Identity.ToString(StringTable),
                DefinitionFilePath = pip.Location.Path.ToString(PathTable),
                DefinitionPath = pip.Location.ToString(PathTable)
            };

            return modulePipDetails;
        }

        /// <summary>
        /// Generates the HashSourceFilePipDetails for a given Pip
        /// </summary>
        public HashSourceFilePipDetails GenerateHashSourceFilePipDetails(HashSourceFile pip)
        {
            HashSourceFilePipDetails hashSourceFilePipDetails = new HashSourceFilePipDetails
            {
                FileHashed = pip.Artifact.Path.ToString(PathTable)
            };

            return hashSourceFilePipDetails;
        }

        /// <summary>
        /// Generates the SealDirectoryPipDetails for a given Pip
        /// </summary>
        public SealDirectoryPipDetails GenerateSealDirectoryPipDetails(SealDirectory pip)
        {
            SealDirectoryPipDetails sealDirectoryPipDetails = new SealDirectoryPipDetails
            {
                Kind = pip.Kind,
                Scrub = pip.Scrub,
                DirectoryRoot = pip.Directory.Path.ToString(PathTable),
                Contents = pip.Contents.Select(x => x.Path.ToString(PathTable)).ToList()
            };
            sealDirectoryPipDetails.Contents = sealDirectoryPipDetails.Contents.Any() ? sealDirectoryPipDetails.Contents : null;

            return sealDirectoryPipDetails;
        }

        /// <summary>
        /// Generates the WriteFilePipDetails for a given Pip
        /// </summary>
        public WriteFilePipDetails GenerateWriteFilePipDetails(WriteFile pip)
        {
            WriteFilePipDetails writeFilePipDetails = new WriteFilePipDetails
            {
                Destination = pip.Destination.IsValid ? pip.Destination.Path.ToString(PathTable) : null,
                FileEncoding = pip.Encoding,
                Tags = pip.Tags.IsValid ? pip.Tags.Select(tag => tag.ToString(StringTable)).ToList() : null
            };
            writeFilePipDetails.Tags = writeFilePipDetails.Tags.Any() ? writeFilePipDetails.Tags : null;

            return writeFilePipDetails;
        }

        #endregion
    }

}