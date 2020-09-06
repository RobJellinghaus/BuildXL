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
    public class PackedPipGraphSpannableListTests : TemporaryStorageTestBase
    {
        public PackedPipGraphSpannableListTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void SpannableList_can_be_constructed()
        {
            SpannableList<int> list = new SpannableList<int>();

            XAssert.AreEqual(0, list.Count);
            XAssert.AreEqual(0, list.Count());

            XAssert.IsFalse(list.Contains(0));
            XAssert.AreEqual(-1, list.IndexOf(0));
            XAssert.AreEqual(0, list.AsSpan().Length);
        }

        [Fact]
        public void SpannableList_can_be_appended()
        {
            SpannableList<int> list = new SpannableList<int>(1);

            XAssert.AreEqual(0, list.Count);
            XAssert.AreEqual(0, list.Count());

            XAssert.IsFalse(list.Contains(1));
            XAssert.AreEqual(-1, list.IndexOf(1));
            XAssert.AreEqual(0, list.AsSpan().Length);

            list.Add(1);
            XAssert.AreEqual(1, list.Count);
            XAssert.AreEqual(1, list.Count());
            XAssert.IsTrue(list.Contains(1));
            XAssert.AreEqual(0, list.IndexOf(1));
            XAssert.AreEqual(1, list.AsSpan().Length);
            XAssert.AreEqual(1, list.AsSpan()[0]);

            list.Add(2);
            XAssert.AreEqual(2, list.Count);
            XAssert.AreEqual(2, list.Count());
            XAssert.IsTrue(list.Contains(1));
            XAssert.IsTrue(list.Contains(2));
            XAssert.IsFalse(list.Contains(3));
            XAssert.AreEqual(0, list.IndexOf(1));
            XAssert.AreEqual(1, list.IndexOf(2));
            XAssert.AreEqual(2, list.AsSpan().Length);
            XAssert.AreEqual(1, list.AsSpan()[0]);
            XAssert.AreEqual(2, list.AsSpan()[1]);
        }

        [Fact]
        public void SpannableList_can_be_inserted()
        {
            SpannableList<int> list = new SpannableList<int>(1);

            list.Insert(0, 1);
            XAssert.AreEqual(1, list.Count);
            XAssert.AreEqual(1, list.Count());
            XAssert.IsTrue(list.Contains(1));
            XAssert.AreEqual(0, list.IndexOf(1));
            XAssert.AreEqual(1, list.AsSpan().Length);
            XAssert.AreEqual("SpannableList<Int32>[1]{ 1 }", list.ToFullString());

            list.Insert(0, 2);
            XAssert.AreEqual(2, list.Count);
            XAssert.AreEqual(2, list.Count());
            Console.WriteLine($"List: {list.ToFullString()}");
            XAssert.IsTrue(list.Contains(1));
            XAssert.IsTrue(list.Contains(2));
            XAssert.IsFalse(list.Contains(3));
            XAssert.AreEqual(1, list.IndexOf(1));
            XAssert.AreEqual(0, list.IndexOf(2));
            XAssert.AreEqual(2, list.AsSpan().Length);
            XAssert.AreEqual(2, list.AsSpan()[0]);
            XAssert.AreEqual(1, list.AsSpan()[1]);
        }
    }
}
