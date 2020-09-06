// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;

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
        public StringTable(int capacity = DefaultCapacity) : base(capacity)
        {
            // zeroth entry is the empty string; works better with saving/loading than null
            Values.Add("");
        }

        public override void SaveToFile(string directory, string name)
        {
            File.WriteAllLines(Path.Combine(directory, name), Values);
        }

        public override void LoadFromFile(string directory, string name)
        {
            Values.Clear();
            Values.AddRange(File.ReadAllLines(Path.Combine(directory, name)));
        }

        public class CachingBuilder : CachingBuilder<StringComparerNonNull>
        {
            public CachingBuilder(StringTable table) : base(table) { }

            public override StringId GetOrAdd(string value)
            {
                if (value == null) { throw new ArgumentNullException("Cannot insert null string into StringTable"); }
                return base.GetOrAdd(value);
            }
        }
    }
}
