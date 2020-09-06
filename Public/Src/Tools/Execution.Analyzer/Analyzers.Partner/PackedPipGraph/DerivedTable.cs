// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Security.Cryptography;

namespace BuildXL.Execution.Analyzers.PackedPipGraph
{
    /// <summary>
    /// DerivedTable extends a BaseTable with additional data, sharing the same ID range.
    /// </summary>
    /// <remarks>
    /// The DerivedTable entries must only refer to IDs that exist in the base table at the time
    /// when the entry was added to the derived table.
    /// </remarks>
    public class DerivedTable<TId, TValue, TBaseValue, TBaseTable> : ValueTable<TId, TValue>
        where TId : struct, Id<TId>
        where TBaseTable : BaseTable<TId, TBaseValue>
    {
        private readonly List<TValue> m_values = new List<TValue>();

        public readonly TBaseTable BaseTable;

        public DerivedTable(TBaseTable baseTable)
        {
            BaseTable = baseTable;
        }
    }
}
