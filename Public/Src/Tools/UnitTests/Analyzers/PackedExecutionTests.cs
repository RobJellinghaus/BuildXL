// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using BuildXL.Execution.Analyzers.PackedExecution;
using System;
using System.Linq;
using Test.BuildXL.TestUtilities.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace Test.Tool.Analyzers
{
    public class PackedExecutionTests : TemporaryStorageTestBase
    {
        public PackedExecutionTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void PackedExecution_can_be_constructed()
        {
            PackedExecution packedExecution = new PackedExecution();

            XAssert.AreEqual(0, packedExecution.PipTable.Count);
            XAssert.AreEqual(0, packedExecution.FileTable.Count);
            XAssert.AreEqual(0, packedExecution.StringTable.Count);
            XAssert.AreEqual(0, packedExecution.WorkerTable.Count);
        }

        [Fact]
        public void PackedExecution_can_store_pips()
        {
            PackedExecution packedExecution = new PackedExecution();
            PackedExecution.Builder pipGraphBuilder = new PackedExecution.Builder(packedExecution);

            long hash = 1;
            string name = "ShellCommon.Shell.ShellCommon.Shell.Merged.Winmetadata";            
            PipId id = pipGraphBuilder.PipTableBuilder.Add(hash, name, PipType.Process);

            XAssert.AreEqual(1, packedExecution.PipTable.Count);
            XAssert.AreEqual(0, packedExecution.FileTable.Count);
            XAssert.AreEqual(4, packedExecution.StringTable.Count);
            XAssert.AreEqual(0, packedExecution.WorkerTable.Count);

            PipEntry entry = packedExecution.PipTable[id];
            XAssert.AreEqual(hash, entry.SemiStableHash);
            XAssert.AreEqual(name, packedExecution.PipTable.PipNameTable.GetText(entry.Name));
        }

        [Fact]
        public void PackedExecution_can_store_files()
        {
            PackedExecution packedExecution = new PackedExecution();
            PackedExecution.Builder pipGraphBuilder = new PackedExecution.Builder(packedExecution);

            string path = "d:\\os\\bin\\shellcommon\\shell\\merged\\winmetadata\\appresolverux.winmd";
            FileId id = pipGraphBuilder.FileTableBuilder.GetOrAdd(path, 1024 * 1024, default);

            XAssert.AreEqual(0, packedExecution.PipTable.Count);
            XAssert.AreEqual(1, packedExecution.FileTable.Count);
            XAssert.AreEqual(8, packedExecution.StringTable.Count);
            XAssert.AreEqual(0, packedExecution.WorkerTable.Count);

            XAssert.AreEqual(path, packedExecution.FileTable.FileNameTable.GetText(packedExecution.FileTable[id].Path));
        }

        [Fact]
        public void PackedExecution_can_store_workers()
        {
            PackedExecution packedExecution = new PackedExecution();
            PackedExecution.Builder pipGraphBuilder = new PackedExecution.Builder(packedExecution);

            string workerName = "BIGWORKER";
            StringId workerNameId = pipGraphBuilder.StringTableBuilder.GetOrAdd(workerName);
            WorkerId workerId = packedExecution.WorkerTable.Add(workerNameId);
            
            XAssert.AreEqual(0, packedExecution.PipTable.Count);
            XAssert.AreEqual(0, packedExecution.FileTable.Count);
            XAssert.AreEqual(1, packedExecution.StringTable.Count);
            XAssert.AreEqual(1, packedExecution.WorkerTable.Count);

            XAssert.AreEqual(workerName, new string(packedExecution.StringTable[packedExecution.WorkerTable[workerId]]));
        }

        [Fact]
        public void PackedExecution_can_save_and_load()
        {
            PackedExecution packedExecution = new PackedExecution();
            PackedExecution.Builder pipGraphBuilder = new PackedExecution.Builder(packedExecution);

            long hash = 1;
            string name = "ShellCommon.Shell.ShellCommon.Shell.Merged.Winmetadata";
            PipId pipId = pipGraphBuilder.PipTableBuilder.Add(hash, name, PipType.Process);
            string path = "d:\\os\\bin\\shellcommon\\shell\\merged\\winmetadata\\appresolverux.winmd";
            pipGraphBuilder.FileTableBuilder.GetOrAdd(path, 1024 * 1024, pipId);
            string workerName = "BIGWORKER";
            pipGraphBuilder.WorkerTableBuilder.GetOrAdd(workerName);

            XAssert.AreEqual(1, packedExecution.PipTable.Count);
            XAssert.AreEqual(1, packedExecution.FileTable.Count);
            XAssert.AreEqual(13, packedExecution.StringTable.Count);

            packedExecution.SaveToDirectory(TemporaryDirectory);

            PackedExecution pipGraph2 = new PackedExecution();
            pipGraph2.LoadFromDirectory(TemporaryDirectory);

            XAssert.AreEqual(1, pipGraph2.PipTable.Count);
            XAssert.AreEqual(1, pipGraph2.FileTable.Count);
            XAssert.AreEqual(13, pipGraph2.StringTable.Count);

            PipId pipId2 = pipGraph2.PipTable.Ids.First();
            XAssert.AreEqual(name, pipGraph2.PipTable.PipNameTable.GetText(pipGraph2.PipTable[pipId].Name));

            FileId fileId2 = pipGraph2.FileTable.Ids.First();
            XAssert.AreEqual(path, pipGraph2.FileTable.FileNameTable.GetText(pipGraph2.FileTable[fileId2].Path));
            XAssert.AreEqual(pipId2, pipGraph2.FileTable[fileId2].ProducerPip);

            WorkerId workerId2 = pipGraph2.WorkerTable.Ids.First();
            XAssert.AreEqual(workerName, new string(pipGraph2.StringTable[pipGraph2.WorkerTable[workerId2]]));
        }
    }
}
