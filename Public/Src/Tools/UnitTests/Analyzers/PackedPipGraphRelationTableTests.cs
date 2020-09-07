// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using BuildXL.Execution.Analyzers.PackedPipGraph;
using System;
using System.Linq;
using Test.BuildXL.TestUtilities.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace Test.Tool.Analyzers
{
    public class PackedPipGraphRelationTableTests : TemporaryStorageTestBase
    {
        public PackedPipGraphRelationTableTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void RelationTable_can_store_one_relation()
        {
            PackedPipGraph pipGraph = new PackedPipGraph();
            PackedPipGraph.Builder pipGraphBuilder = new PackedPipGraph.Builder(pipGraph);
            string hash = "PipHash";
            string name = "ShellCommon.Shell.ShellCommon.Shell.Merged.Winmetadata";
            PipId pipId = pipGraphBuilder.PipTableBuilder.GetOrAdd(hash, name, new TimeSpan(0, 5, 0));

            RelationTable<PipId, PipId, PipTable, PipTable> relationTable = 
                new RelationTable<PipId, PipId, PipTable, PipTable>(pipGraph.PipTable, pipGraph.PipTable);

            XAssert.AreEqual(0, relationTable[pipId]);

            relationTable.AddRelations(pipId, new[] { pipId }.AsSpan());

            XAssert.AreEqual(1, relationTable[pipId]);

            ReadOnlySpan<PipId> relations = relationTable.GetRelations(pipId);

            XAssert.AreEqual(relations[0], pipId);
        }
    }
}
