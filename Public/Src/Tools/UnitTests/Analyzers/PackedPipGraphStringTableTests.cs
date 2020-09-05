// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using BuildXL.Execution.Analyzers.PackedPipGraph;
using System;
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

            StringTable.CachingBuilder builder = new StringTable.CachingBuilder(stringTable);

            StringId id = builder.GetOrAdd("a");
            StringId id2 = builder.GetOrAdd("a");
            XAssert.IsTrue(id == id2);
            XAssert.IsTrue("a" == stringTable[id]);
        }

        [Fact]
        public void StringTable_can_store_two_elements()
        {
            StringTable stringTable = new StringTable();

            StringTable.CachingBuilder builder = new StringTable.CachingBuilder(stringTable);

            StringId id = builder.GetOrAdd("a");
            StringId id2 = builder.GetOrAdd("b");
            XAssert.IsTrue(id != id2);
            XAssert.IsTrue("b" == stringTable[id2]);
        }
    }
}
