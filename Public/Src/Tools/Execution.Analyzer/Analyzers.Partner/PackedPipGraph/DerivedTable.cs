// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

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

        public void Set(TId id, TValue value)
        {
            if (id.Equals(default)) { throw new ArgumentException("Cannot set default ID"); }
            if (id.FromId() > BaseTable.Count()) { throw new ArgumentException($"ID {id.FromId()} is out of range of base table {BaseTable.Count()}"); }

            if (BaseTable.Count() > m_values.Capacity)
            {
                // grow a little ahead of the base table
                m_values.Capacity = (int)(BaseTable.Count() * 1.2);
            }

            m_values[id.FromId()] = value;
        }
    }
}
