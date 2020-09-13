// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BuildXL.Execution.Analyzers.PackedExecution;
using BuildXL.Pips;
using BuildXL.Pips.Operations;
using BuildXL.ToolSupport;

namespace BuildXL.Execution.Analyzer
{
    // The PackedExecution has a quite different implementation of the very similar concepts,
    // so in this analyzer we refer to them separately.
    // We use the ugly but unmissable convention of "P_" meaning "PackedExecution" and "B_" meaning "BuildXL".

    using B_PipId = BuildXL.Pips.PipId;
    using B_PipType = BuildXL.Pips.Operations.PipType;

    using P_PipId = BuildXL.Execution.Analyzers.PackedExecution.PipId;
    using P_PipTable = BuildXL.Execution.Analyzers.PackedExecution.PipTable;
    using P_PipType = BuildXL.Execution.Analyzers.PackedExecution.PipType;
    using P_StringTable = BuildXL.Execution.Analyzers.PackedExecution.StringTable;

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

        public PackedExecutionExporter(AnalysisInput input, string outputDirectoryPath)
            : base(input)
        {
            m_outputDirectoryPath = outputDirectoryPath;

            Console.WriteLine($"PackedExecutionExporter: Constructed at {DateTime.Now}.");
        }

        public override int Analyze()
        {
            Console.WriteLine($"PackedExecutionExporter: Starting export at {DateTime.Now}.");

            if (!Directory.Exists(m_outputDirectoryPath))
            {
                Directory.CreateDirectory(m_outputDirectoryPath);
            }

            PackedExecution pipGraph = new PackedExecution();
            P_StringTable.CachingBuilder stringBuilder = new P_StringTable.CachingBuilder(pipGraph.StringTable);
            P_PipTable.Builder pipBuilder = new P_PipTable.Builder(pipGraph.PipTable, stringBuilder);

            List<PipReference> pipList =
                PipGraph.AsPipReferences(PipTable.StableKeys, PipQueryContext.PipGraphRetrieveAllPips).ToList();

            // Populate the PipTable with all known PipIds, defining a mapping from the BXL PipIds to the PackedExecution PipIds.
            for (int i = 0; i < pipList.Count; i++)
            {
                // Each pip gets a P_PipId that is one plus its index in this list, since zero is never a PackedExecution ID value.
                // This seems to be exactly how B_PipId works as well, but we verify to be sure we can rely on this invariant.
                P_PipId graphPipId = AddPip(pipList[i], pipBuilder);
                if (graphPipId.Value != pipList[i].PipId.Value)
                {
                    throw new ArgumentException($"Graph pip ID {graphPipId.Value} does not equal BXL pip ID {pipList[i].PipId.Value}");
                }
            }

            Console.WriteLine($"PackedExecutionExporter: Added {PipGraph.PipCount} pips at {DateTime.Now}.");

            // Now that all pips are loaded, construct the PipDependencies RelationTable.
            pipGraph.ConstructRelationTables();

            // and now do it again with the dependencies, now that everything is established.
            // Since we added all the pips in pipList order to PipTable, we can traverse them again in the same order
            // to build the relation.
            SpannableList<P_PipId> buffer = new SpannableList<P_PipId>(); // to accumulate the IDs we add to the relation
            for (int i = 0; i < pipList.Count; i++)
            {
                AddPipDependencies(
                    new P_PipId(i + 1),
                    pipList[i],
                    pipGraph.PipDependencies,
                    buffer);
            }

            Console.WriteLine($"PackedExecutionExporter: Added {pipGraph.PipDependencies.MultiValueCount} total dependencies at {DateTime.Now}.");

            // and write it out
            pipGraph.SaveToDirectory(m_outputDirectoryPath);

            Console.WriteLine($"PackedExecutionExporter: Wrote out pip graph at {DateTime.Now}.");

            return 0;
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
            P_PipId g_pipId, 
            PipReference pipReference,
            RelationTable<P_PipId, P_PipId> relationTable,
            SpannableList<P_PipId> buffer)
        {
            IEnumerable<P_PipId> pipDependencies = PipGraph
                .RetrievePipReferenceImmediateDependencies(pipReference.PipId, null)
                .Where(pipRef => pipRef.PipType != B_PipType.HashSourceFile)
                .Select(pid => pid.PipId.Value)
                .Distinct()
                .OrderBy(pid => pid)
                .Select(pid => new P_PipId((int)pid));

            buffer.Clear();
            buffer.AddRange(pipDependencies);
            relationTable.Add(buffer.AsSpan());
        }
    }

}