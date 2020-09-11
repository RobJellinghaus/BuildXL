// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace BuildXL.Execution.Analyzers.PackedPipGraph
{
    /// <summary>
    /// Boilerplate ID type to avoid ID confusion in code.
    /// </summary>
    public struct WorkerId : Id<WorkerId>, IEqualityComparer<WorkerId>
    {
        public readonly int Value;
        public WorkerId(int value) { Value = value; }
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
    /// The StringId value is the worker's MachineName.
    /// </remarks>
    public class WorkerTable : SingleValueTable<WorkerId, StringId>
    {
        /// <summary>
        /// The table containing the strings referenced by this WorkerTable.
        /// </summary>
        /// <remarks>
        /// The WorkerTable does not own this StringTable; it is probably shared.
        /// </remarks>
        public readonly StringTable StringTable;

        public WorkerTable(StringTable stringTable, int capacity = DefaultCapacity) : base(capacity)
        {
            StringTable = stringTable;
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
