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
    public class PackedPipGraphTests : TemporaryStorageTestBase
    {
        public PackedPipGraphTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void PackedPipGraph_can_be_constructed()
        {
            PackedPipGraph pipGraph = new PackedPipGraph();

            XAssert.AreEqual(0, pipGraph.PipTable.Count);
            XAssert.AreEqual(0, pipGraph.FileTable.Count);
            XAssert.AreEqual(0, pipGraph.StringTable.Count);
        }

        [Fact]
        public void PackedPipGraph_can_store_pips()
        {
            PackedPipGraph pipGraph = new PackedPipGraph();
            PackedPipGraph.Builder pipGraphBuilder = new PackedPipGraph.Builder(pipGraph);

            string hash = "PipHash";
            string name = "ShellCommon.Shell.ShellCommon.Shell.Merged.Winmetadata";            
            PipId id = pipGraphBuilder.PipTableBuilder.GetOrAdd(hash, name, new TimeSpan(0, 5, 0));

            XAssert.AreEqual(1, pipGraph.PipTable.Count);
            XAssert.AreEqual(0, pipGraph.FileTable.Count);
            XAssert.AreEqual(5, pipGraph.StringTable.Count);

            PipEntry entry = pipGraph.PipTable[id];
            XAssert.AreEqual(hash, pipGraph.StringTable[entry.Hash]);
            XAssert.AreEqual(name, pipGraph.PipTable.PipNameTable.GetText(entry.Name));
        }

        [Fact]
        public void PackedPipGraph_can_store_files()
        {
            PackedPipGraph pipGraph = new PackedPipGraph();
            PackedPipGraph.Builder pipGraphBuilder = new PackedPipGraph.Builder(pipGraph);

            string path = "d:\\os\\bin\\shellcommon\\shell\\merged\\winmetadata\\appresolverux.winmd";
            FileId id = pipGraphBuilder.FileTableBuilder.GetOrAdd(path, 1024 * 1024);

            XAssert.AreEqual(0, pipGraph.PipTable.Count);
            XAssert.AreEqual(1, pipGraph.FileTable.Count);
            XAssert.AreEqual(8, pipGraph.StringTable.Count);

            XAssert.AreEqual(path, pipGraph.FileTable.FileNameTable.GetText(pipGraph.FileTable[id].Name));
        }
    }
}
