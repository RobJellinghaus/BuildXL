// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using BuildXL.Execution.Analyzers.PackedPipGraph;
using System;
using Test.BuildXL.TestUtilities.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace Test.Tool.Analyzers
{
    public class PackedPipGraphNameTableTests : TemporaryStorageTestBase
    {
        public PackedPipGraphNameTableTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void NameTable_can_store_one_singular_element()
        {
            StringTable stringTable = new StringTable();
            StringTable.Builder stringTableBuilder = new StringTable.Builder(stringTable);

            NameTable nameTable = new NameTable('.', stringTable);
            NameTable.Builder nameTableBuilder = new NameTable.Builder(nameTable, stringTableBuilder);

            NameId id = nameTableBuilder.GetOrAdd("a");
            NameId id2 = nameTableBuilder.GetOrAdd("a");
            XAssert.IsTrue(id.Equals(id2));
            XAssert.AreEqual("a", nameTable.GetText(id));
            XAssert.AreEqual(1, nameTable.Length(id));
        }

        [Fact]
        public void NameTable_can_store_two_elements()
        {
            StringTable stringTable = new StringTable();
            StringTable.Builder stringTableBuilder = new StringTable.Builder(stringTable);

            NameTable nameTable = new NameTable('.', stringTable);
            NameTable.Builder nameTableBuilder = new NameTable.Builder(nameTable, stringTableBuilder);

            NameId id = nameTableBuilder.GetOrAdd("a");
            NameId id2 = nameTableBuilder.GetOrAdd("b");
            XAssert.IsFalse(id.Equals(id2));
            XAssert.AreEqual("b", nameTable.GetText(id2));
            XAssert.AreEqual(1, nameTable.Length(id2));
        }
    }
}
