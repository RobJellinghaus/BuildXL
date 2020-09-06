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
    public class DerivedTable<TId, TValue, TBaseTable> : ValueTable<TId, TValue>
        where TId : struct, Id<TId>
        where TValue : unmanaged
        where TBaseTable : Table<TId>
    {
        public readonly TBaseTable BaseTable;

        public DerivedTable(TBaseTable baseTable)
        {
            BaseTable = baseTable;
        }

        public void Set(TId id, TValue value)
        {
            if (id.Equals(default)) { throw new ArgumentException("Cannot set default ID"); }
            if (id.FromId() > BaseTable.Count) { throw new ArgumentException($"ID {id.FromId()} is out of range of base table {BaseTable.Count}"); }

            if (BaseTable.Count > Values.Capacity)
            {
                // grow a little ahead of the base table
                Values.Capacity = (int)(BaseTable.Count * 1.2);
            }

            Values[id.FromId()] = value;
        }

        public override void SaveToFile(string directory, string name)
        {
            FileSpanUtilities.SaveToFile<TValue>(directory, name, Values);
        }

        public override void LoadFromFile(string directory, string name)
        {
            Values.Clear();
            Values.AddRange(FileSpanUtilities.LoadFromFile<TValue>(directory, name));
        }
    }
}
