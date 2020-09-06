﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using BuildXL.Utilities;

namespace BuildXL.Execution.Analyzers.PackedPipGraph
{
    /// <summary>
    /// Boilerplate ID type to avoid ID confusion in code.
    /// </summary>
    public struct PipId : Id<PipId>, IEqualityComparer<PipId>
    {
        internal readonly int Value;
        internal PipId(int value) { Value = value; }
        int Id<PipId>.FromId() => Value;
        PipId Id<PipId>.ToId(int value) => new PipId(value);
        public override string ToString() => $"PipId[{Value}]";
        public bool Equals([AllowNull] PipId x, [AllowNull] PipId y) => x.Value == y.Value;
        public int GetHashCode([DisallowNull] PipId obj) => obj.Value;
    }

    public struct PipEntry
    {
        public readonly StringId Hash;
        public readonly NameId Name;
        // TODO: do we want ExecutionTime to be in a DerivedTable? (we may, if we want to experiment with it)
        public readonly TimeSpan ExecutionTime;
        public PipEntry(StringId hash, NameId name, TimeSpan executionTime) { Hash = hash; Name = name; ExecutionTime = executionTime; }

        public struct EqualityComparer : IEqualityComparer<PipEntry>
        {
            public bool Equals(PipEntry x, PipEntry y) => x.Hash.Equals(y.Hash);
            public int GetHashCode([DisallowNull] PipEntry obj) => obj.Hash.GetHashCode();
        }
    }

    /// <summary>
    /// Table of pip data.
    /// </summary>
    public class PipTable : BaseTable<PipId, PipEntry>
    {
        public PipTable()
        {
        }
        public class CachingBuilder : CachingBuilder<PipEntry.EqualityComparer>
        {
            public CachingBuilder(PipTable table) : base(table) { }
        }
    }
}
