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
            PipId id = pipGraphBuilder.PipTableBuilder.GetOrAdd(hash, name, PipType.Process);

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

        [Fact]
        public void PackedPipGraph_can_save_and_load()
        {
            PackedPipGraph pipGraph = new PackedPipGraph();
            PackedPipGraph.Builder pipGraphBuilder = new PackedPipGraph.Builder(pipGraph);

            string path = "d:\\os\\bin\\shellcommon\\shell\\merged\\winmetadata\\appresolverux.winmd";
            pipGraphBuilder.FileTableBuilder.GetOrAdd(path, 1024 * 1024);
            string hash = "PipHash";
            string name = "ShellCommon.Shell.ShellCommon.Shell.Merged.Winmetadata";
            pipGraphBuilder.PipTableBuilder.GetOrAdd(hash, name, PipType.Process);

            XAssert.AreEqual(1, pipGraph.PipTable.Count);
            XAssert.AreEqual(1, pipGraph.FileTable.Count);
            XAssert.AreEqual(13, pipGraph.StringTable.Count);

            pipGraph.SaveToDirectory(TemporaryDirectory);

            PackedPipGraph pipGraph2 = new PackedPipGraph();
            pipGraph2.LoadFromDirectory(TemporaryDirectory);

            XAssert.AreEqual(1, pipGraph2.PipTable.Count);
            XAssert.AreEqual(1, pipGraph2.FileTable.Count);
            XAssert.AreEqual(13, pipGraph2.StringTable.Count);
            FileId fileId = pipGraph2.FileTable.Ids.First();
            XAssert.AreEqual(path, pipGraph2.FileTable.FileNameTable.GetText(pipGraph2.FileTable[fileId].Name));
            PipId pipId = pipGraph2.PipTable.Ids.First();
            XAssert.AreEqual(name, pipGraph2.PipTable.PipNameTable.GetText(pipGraph2.PipTable[pipId].Name));
        }
    }
}
