// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Security.Cryptography;
using BuildXL.Execution.Analyzer.JPath;
using Google.Protobuf.WellKnownTypes;

namespace BuildXL.Execution.Analyzers.PackedPipGraph
{
    /// <summary>
    /// Boilerplate ID type to avoid ID confusion in code.
    /// </summary>
    public struct StringId : Id<StringId>
    {
        internal readonly int Value;
        internal StringId(int value) { Value = value; }
        int Id<StringId>.FromId() => Value;
        StringId Id<StringId>.ToId(int value) => new StringId(value);

        public static bool operator==(StringId left, StringId right)
        {
            return left.Value == right.Value;
        }

        public static bool operator!=(StringId left, StringId right)
        {
            return !(left == right);
        }
    }

    /// <summary>
    /// Table of unique strings.
    /// </summary>
    /// <remarks>
    /// Yes, that's all there is to it :-) Use a StringTable.CachingBuilder to populate one of these.
    /// </remarks>
    public class StringTable : BaseTable<StringId, string>
    {
        public StringTable()
        {
        }
    }
}
