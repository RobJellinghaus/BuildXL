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
            long hash = 1;
            string name = "ShellCommon.Shell.ShellCommon.Shell.Merged.Winmetadata";
            PipId pipId = pipGraphBuilder.PipTableBuilder.Add(hash, name, PipType.Process);

            pipGraph.ConstructRelationTables();

            RelationTable<PipId, PipId, PipTable, PipTable> relationTable = pipGraph.PipDependencies;

            relationTable.Add(new[] { pipId }.AsSpan());

            XAssert.AreEqual(1, relationTable[pipId].Length);

            ReadOnlySpan<PipId> relations = relationTable[pipId];
            XAssert.AreEqual(pipId, relations[0]);

            RelationTable<PipId, PipId, PipTable, PipTable> inverseRelationTable = relationTable.Invert();

            XAssert.AreEqual(1, inverseRelationTable[pipId].Length);
            XAssert.AreEqual(pipId, inverseRelationTable[pipId][0]);
        }

        [Fact]
        public void RelationTable_can_store_multiple_relations()
        {
            PackedPipGraph pipGraph = new PackedPipGraph();
            PackedPipGraph.Builder pipGraphBuilder = new PackedPipGraph.Builder(pipGraph);
            long hash = 1;
            string name = "ShellCommon.Shell.ShellCommon.Shell.Merged.Winmetadata";
            PipId pipId = pipGraphBuilder.PipTableBuilder.Add(hash, name, PipType.Process);
            PipId pipId2 = pipGraphBuilder.PipTableBuilder.Add(hash + 1, $"{name}2", PipType.Process);
            PipId pipId3 = pipGraphBuilder.PipTableBuilder.Add(hash + 2, $"{name}3", PipType.Process);

            XAssert.AreNotEqual(pipId, pipId2);
            XAssert.AreNotEqual(pipId, pipId3);
            XAssert.AreNotEqual(pipId2, pipId3);

            pipGraph.ConstructRelationTables();

            RelationTable<PipId, PipId, PipTable, PipTable> relationTable = pipGraph.PipDependencies;

            relationTable.Add(new[] { pipId2, pipId3 }.AsSpan());

            XAssert.AreEqual(2, relationTable[pipId].Length);

            ReadOnlySpan<PipId> relations = relationTable[pipId];

            XAssert.AreEqual(pipId2, relations[0]);
            XAssert.AreEqual(pipId3, relations[1]);

            relationTable.Add(new[] { pipId }.AsSpan());

            XAssert.AreEqual(1, relationTable[pipId2].Length);

            relationTable.Add(new[] { pipId, pipId2, pipId3 }.AsSpan());

            XAssert.AreEqual(3, relationTable[pipId3].Length);
            XAssert.AreArraysEqual(new[] { pipId2, pipId3 }, relationTable[pipId].ToArray(), true);
            XAssert.AreArraysEqual(new[] { pipId, pipId2, pipId3 }, relationTable[pipId3].ToArray(), true);

            RelationTable<PipId, PipId, PipTable, PipTable> inverseRelationTable = relationTable.Invert();

            XAssert.AreArraysEqual(new[] { pipId2, pipId3 }, inverseRelationTable[pipId].ToArray(), true);
            XAssert.AreArraysEqual(new[] { pipId, pipId3 }, inverseRelationTable[pipId2].ToArray(), true);
            XAssert.AreArraysEqual(new[] { pipId, pipId3 }, inverseRelationTable[pipId3].ToArray(), true);
        }
    }
}
