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

        [Fact]
        public void RelationTable_can_store_multiple_relations()
        {
            PackedPipGraph pipGraph = new PackedPipGraph();
            PackedPipGraph.Builder pipGraphBuilder = new PackedPipGraph.Builder(pipGraph);
            string hash = "PipHash";
            string name = "ShellCommon.Shell.ShellCommon.Shell.Merged.Winmetadata";
            PipId pipId = pipGraphBuilder.PipTableBuilder.GetOrAdd(hash, name, new TimeSpan(0, 5, 0));
            PipId pipId2 = pipGraphBuilder.PipTableBuilder.GetOrAdd($"{hash}2", $"{name}2", new TimeSpan(0, 10, 0));
            PipId pipId3 = pipGraphBuilder.PipTableBuilder.GetOrAdd($"{hash}3", $"{name}3", new TimeSpan(0, 10, 0));

            XAssert.AreNotEqual(pipId, pipId2);
            XAssert.AreNotEqual(pipId, pipId3);
            XAssert.AreNotEqual(pipId2, pipId3);

            RelationTable<PipId, PipId, PipTable, PipTable> relationTable =
                new RelationTable<PipId, PipId, PipTable, PipTable>(pipGraph.PipTable, pipGraph.PipTable);

            XAssert.AreEqual(0, relationTable[pipId]);

            relationTable.AddRelations(pipId, new[] { pipId2, pipId3 }.AsSpan());

            XAssert.AreEqual(2, relationTable[pipId]);

            ReadOnlySpan<PipId> relations = relationTable.GetRelations(pipId);

            XAssert.AreEqual(pipId2, relations[0]);
            XAssert.AreEqual(pipId3, relations[1]);

            relationTable.AddRelations(pipId2, new[] { pipId }.AsSpan());

            XAssert.AreEqual(1, relationTable[pipId2]);

            relationTable.AddRelations(pipId3, new[] { pipId, pipId2, pipId3 }.AsSpan());

            XAssert.AreEqual(3, relationTable[pipId3]);
            XAssert.AreArraysEqual(new[] { pipId2, pipId3 }, relationTable.GetRelations(pipId).ToArray(), true);
            XAssert.AreArraysEqual(new[] { pipId, pipId2, pipId3 }, relationTable.GetRelations(pipId3).ToArray(), true);
        }
    }
}
