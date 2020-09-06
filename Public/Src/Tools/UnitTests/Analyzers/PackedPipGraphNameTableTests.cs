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
    public class PackedPipGraphNameTableTests : TemporaryStorageTestBase
    {
        public PackedPipGraphNameTableTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void NameTable_can_store_one_singular_element()
        {
            StringTable stringTable = new StringTable();
            StringTable.CachingBuilder stringTableBuilder = new StringTable.CachingBuilder(stringTable);

            NameTable nameTable = new NameTable('.', stringTable);
            NameTable.Builder nameTableBuilder = new NameTable.Builder(nameTable, stringTableBuilder);

            XAssert.AreEqual(0, nameTable.Count());
            XAssert.AreEqual(0, nameTable.Ids.Count());

            NameId id = nameTableBuilder.GetOrAdd("a");
            NameId id2 = nameTableBuilder.GetOrAdd("a");

            XAssert.IsTrue(id.Equals(id2));
            XAssert.AreEqual("a", nameTable.GetText(id));
            XAssert.AreEqual(1, nameTable.Length(id));
            XAssert.AreEqual(1, nameTable.Count());
            XAssert.AreEqual(1, nameTable.Ids.Count());
        }

        [Fact]
        public void NameTable_can_store_two_elements()
        {
            StringTable stringTable = new StringTable();
            StringTable.CachingBuilder stringTableBuilder = new StringTable.CachingBuilder(stringTable);

            NameTable nameTable = new NameTable('.', stringTable);
            NameTable.Builder nameTableBuilder = new NameTable.Builder(nameTable, stringTableBuilder);

            NameId id = nameTableBuilder.GetOrAdd("a");
            NameId id2 = nameTableBuilder.GetOrAdd("b");

            XAssert.IsFalse(id.Equals(id2));
            XAssert.AreEqual("b", nameTable.GetText(id2));
            XAssert.AreEqual(1, nameTable.Length(id2));
            XAssert.AreEqual(2, nameTable.Count());
            XAssert.AreEqual(2, nameTable.Ids.Count());
        }

        [Fact]
        public void NameTable_can_store_one_complex_element()
        {
            StringTable stringTable = new StringTable();
            StringTable.CachingBuilder stringTableBuilder = new StringTable.CachingBuilder(stringTable);

            NameTable nameTable = new NameTable('.', stringTable);
            NameTable.Builder nameTableBuilder = new NameTable.Builder(nameTable, stringTableBuilder);

            NameId id = nameTableBuilder.GetOrAdd("a.b");
            NameId id2 = nameTableBuilder.GetOrAdd("a.b");

            XAssert.IsTrue(id.Equals(id2));
            XAssert.AreEqual("a.b", nameTable.GetText(id));
            XAssert.AreEqual(3, nameTable.Length(id));
            XAssert.AreEqual(2, nameTable.Count());
            XAssert.AreEqual(2, nameTable.Ids.Count());
        }

        [Fact]
        public void NameTable_can_store_two_complex_elements()
        {
            StringTable stringTable = new StringTable();
            StringTable.CachingBuilder stringTableBuilder = new StringTable.CachingBuilder(stringTable);

            NameTable nameTable = new NameTable('.', stringTable);
            NameTable.Builder nameTableBuilder = new NameTable.Builder(nameTable, stringTableBuilder);

            NameId id = nameTableBuilder.GetOrAdd("a.b");
            NameId id2 = nameTableBuilder.GetOrAdd("a.ccc");

            XAssert.IsFalse(id.Equals(id2));
            XAssert.AreEqual("a.b", nameTable.GetText(id));
            XAssert.AreEqual(3, nameTable.Length(id));
            XAssert.AreEqual("a.ccc", nameTable.GetText(id2));
            XAssert.AreEqual(5, nameTable.Length(id2));
            XAssert.AreEqual(3, nameTable.Count());
            XAssert.AreEqual(3, nameTable.Ids.Count());
        }

        [Fact]
        public void NameTable_can_store_three_complex_elements()
        {
            StringTable stringTable = new StringTable();
            StringTable.CachingBuilder stringTableBuilder = new StringTable.CachingBuilder(stringTable);

            NameTable nameTable = new NameTable('.', stringTable);
            NameTable.Builder nameTableBuilder = new NameTable.Builder(nameTable, stringTableBuilder);

            NameId id = nameTableBuilder.GetOrAdd("a.b.c");
            NameId id2 = nameTableBuilder.GetOrAdd("a.b.d.e");
            NameId id3 = nameTableBuilder.GetOrAdd("a.f.g.h");

            XAssert.IsFalse(id.Equals(id2));
            XAssert.IsFalse(id.Equals(id3));
            XAssert.IsFalse(id2.Equals(id3));
            XAssert.AreEqual("a.b.c", nameTable.GetText(id));
            XAssert.AreEqual(5, nameTable.Length(id));
            XAssert.AreEqual("a.b.d.e", nameTable.GetText(id2));
            XAssert.AreEqual(7, nameTable.Length(id2));
            XAssert.AreEqual("a.f.g.h", nameTable.GetText(id3));
            XAssert.AreEqual(7, nameTable.Length(id3));
            XAssert.AreEqual(8, nameTable.Count());
            XAssert.AreEqual(8, nameTable.Ids.Count());
        }
    }
}
