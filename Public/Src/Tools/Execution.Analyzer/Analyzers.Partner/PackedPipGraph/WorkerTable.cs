// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace BuildXL.Execution.Analyzers.PackedPipGraph
{
    /// <summary>
    /// Boilerplate ID type to avoid ID confusion in code.
    /// </summary>
    public struct WorkerId : Id<WorkerId>, IEqualityComparer<WorkerId>
    {
        internal readonly int Value;
        internal WorkerId(int value) { Value = value; }
        int Id<WorkerId>.FromId() => Value;
        WorkerId Id<WorkerId>.ToId(int value) => new WorkerId(value);
        public override string ToString() => $"WorkerId[{Value}]";
        public bool Equals([AllowNull] WorkerId x, [AllowNull] WorkerId y) => x.Value == y.Value;
        public int GetHashCode([DisallowNull] WorkerId obj) => obj.Value;
    }

    /// <summary>
    /// Tracks the workers in a build.
    /// </summary>
    /// <remarks>
    /// The value is the worker's MachineName.
    /// </remarks>
    public class WorkerTable : BaseUnmanagedTable<WorkerId, StringId>
    {
        public WorkerTable(int capacity = -1) : base(capacity)
        {
        }

        public class CachingBuilder : CachingBuilder<StringId>
        {
            private readonly StringTable.CachingBuilder m_stringTableBuilder;

            public CachingBuilder(WorkerTable workerTable, StringTable.CachingBuilder stringTableBuilder) 
                : base(workerTable)
            {
                m_stringTableBuilder = stringTableBuilder;
            }

            public WorkerId GetOrAdd(string workerMachineName)
            {
                StringId stringId = m_stringTableBuilder.GetOrAdd(workerMachineName);
                return GetOrAdd(stringId);
            }
        }
    }
}
