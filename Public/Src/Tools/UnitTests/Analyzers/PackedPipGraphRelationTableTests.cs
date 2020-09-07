// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using BuildXL.Execution.Analyzers.PackedPipGraph;
using System;
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
            PipId pipId = pipGraphBuilder.PipTableBuilder.GetOrAdd(hash, name, PipType.Process);

            RelationTable<PipId, PipId, PipTable, PipTable> relationTable =
                new RelationTable<PipId, PipId, PipTable, PipTable>(pipGraph.PipTable, pipGraph.PipTable);

            XAssert.AreEqual(0, relationTable[pipId]);

            relationTable.AddRelations(pipId, new[] { pipId }.AsSpan());

            XAssert.AreEqual(1, relationTable[pipId]);

            ReadOnlySpan<PipId> relations = relationTable.GetRelations(pipId);
            XAssert.AreEqual(pipId, relations[0]);

            RelationTable<PipId, PipId, PipTable, PipTable> inverseRelationTable = relationTable.Invert();

            XAssert.AreEqual(1, inverseRelationTable[pipId]);
            XAssert.AreEqual(pipId, inverseRelationTable.GetRelations(pipId)[0]);
        }

        [Fact]
        public void RelationTable_can_store_multiple_relations()
        {
            PackedPipGraph pipGraph = new PackedPipGraph();
            PackedPipGraph.Builder pipGraphBuilder = new PackedPipGraph.Builder(pipGraph);
            string hash = "PipHash";
            string name = "ShellCommon.Shell.ShellCommon.Shell.Merged.Winmetadata";
            PipId pipId = pipGraphBuilder.PipTableBuilder.GetOrAdd(hash, name, PipType.Process);
            PipId pipId2 = pipGraphBuilder.PipTableBuilder.GetOrAdd($"{hash}2", $"{name}2", PipType.Process);
            PipId pipId3 = pipGraphBuilder.PipTableBuilder.GetOrAdd($"{hash}3", $"{name}3", PipType.Process);

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

            RelationTable<PipId, PipId, PipTable, PipTable> inverseRelationTable = relationTable.Invert();

            XAssert.AreArraysEqual(new[] { pipId2, pipId3 }, inverseRelationTable.GetRelations(pipId).ToArray(), true);
            XAssert.AreArraysEqual(new[] { pipId, pipId3 }, inverseRelationTable.GetRelations(pipId2).ToArray(), true);
            XAssert.AreArraysEqual(new[] { pipId, pipId3 }, inverseRelationTable.GetRelations(pipId3).ToArray(), true);
        }
    }
}
