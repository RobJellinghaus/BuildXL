// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using BuildXL.Execution.Analyzer.JPath;
using Google.Protobuf.WellKnownTypes;

namespace BuildXL.Execution.Analyzers.PackedPipGraph
{
    /// <summary>
    /// Boilerplate ID type to avoid ID confusion in code.
    /// </summary>
    public struct StringId : Id<StringId>, IEqualityComparer<StringId>
    {
        internal readonly int Value;
        internal StringId(int value) { Value = value; }
        int Id<StringId>.FromId() => Value;
        StringId Id<StringId>.ToId(int value) => new StringId(value);
        public override string ToString() => $"StringId[{Value}]";
        public bool Equals([AllowNull] StringId x, [AllowNull] StringId y) => x.Value == y.Value;
        public int GetHashCode([DisallowNull] StringId obj) => obj.Value;
    }

    public struct StringComparerNonNull : IEqualityComparer<string>
    {
        public bool Equals([AllowNull] string x, [AllowNull] string y) => StringComparer.InvariantCulture.Equals(x, y);
        public int GetHashCode([DisallowNull] string obj) => StringComparer.InvariantCulture.GetHashCode(obj);
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

        public class CachingBuilder : CachingBuilder<StringComparerNonNull>
        {
            public CachingBuilder(StringTable table) : base(table) { }
        }
    }
}
