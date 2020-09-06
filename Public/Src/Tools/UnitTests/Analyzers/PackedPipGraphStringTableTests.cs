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
    public class PackedPipGraphStringTableTests : TemporaryStorageTestBase
    {
        public PackedPipGraphStringTableTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void StringTable_can_store_one_element()
        {
            StringTable stringTable = new StringTable();

            StringTable.Builder builder = new StringTable.Builder(stringTable);

            StringId id = builder.GetOrAdd("a");
            StringId id2 = builder.GetOrAdd("a");
            XAssert.IsTrue(id.Equals(id2));
            XAssert.AreEqual("a", stringTable[id]);
            XAssert.AreEqual(1, stringTable.Count());
            XAssert.AreEqual(1, stringTable.Ids.Count());
        }

        [Fact]
        public void StringTable_can_store_two_elements()
        {
            StringTable stringTable = new StringTable();

            StringTable.Builder builder = new StringTable.Builder(stringTable);

            StringId id = builder.GetOrAdd("a");
            StringId id2 = builder.GetOrAdd("b");
            XAssert.IsFalse(id.Equals(id2));
            XAssert.AreEqual("b", stringTable[id2]);
            XAssert.AreEqual(2, stringTable.Count());
            XAssert.AreEqual(2, stringTable.Ids.Count());
        }
    }
}
