// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace BuildXL.Execution.Analyzers.PackedPipGraph
{
    /// <summary>
    /// DerivedTable extends a BaseTable with additional (unmanaged) data, sharing the same ID range.
    /// </summary>
    /// <remarks>
    /// Every DerivedTable value is (naturally for C#) initially the default value.
    /// </remarks>
    public class DerivedTable<TId, TValue, TBaseTable> : ValueTable<TId, TValue>
        where TId : struct, Id<TId>
        where TValue : unmanaged
        where TBaseTable : Table<TId>
    {
        public readonly TBaseTable BaseTable;

        protected readonly SpannableList<TValue> Values;

        public DerivedTable(TBaseTable baseTable)
        {
            BaseTable = baseTable;
            Values = new SpannableList<TValue>(Count + 1);
            Values.Add(default);
            EnsureCount();
        }

        protected override IList<TValue> GetValues() => Values;

        public override int Count => BaseTable.Count;

        public override IEnumerable<TId> Ids => BaseTable.Ids;

        /// <summary>
        /// Get the value at this id.
        /// </summary>
        /// <remarks>
        /// Because we support growing base tables while also growing derived tables,
        /// it is possible the base table got bigger and that we may not have enough
        /// items allocated to match it in our capacity. Hence this method ensures
        /// the derived table is as long as the base table. If this becomes expensive,
        /// we may reconsider supporting building derived tables concurrently with 
        /// base tables.
        /// </remarks>
        public override TValue this[TId id]
        {
            get
            {
                EnsureCount();
                return base[id];
            }
        }

        public void Set(TId id, TValue value)
        {
            if (id.Equals(default)) { throw new ArgumentException("Cannot set default ID"); }
            if (id.FromId() > BaseTable.Count) { throw new ArgumentException($"ID {id.FromId()} is out of range of base table {BaseTable.Count}"); }

            EnsureCount();

            Values[id.FromId()] = value;
        }

        /// <summary>
        /// Ensure this table stores at least as many elements as the base table.
        /// </summary>
        private void EnsureCount()
        {
            // The +1 here is to account for the mandatory initial zero entry in all tables; this method commingles
            // looking at the table count (which hides this entry) and looking at the local list count (which must take
            // that entry into account).
            if (BaseTable.Count + 1 > Values.Count)
            {
                Values.AddRange(Enumerable.Repeat<TValue>(default, BaseTable.Count - Values.Count + 1));
            }
        }

        public override void SaveToFile(string directory, string name)
        {
            FileSpanUtilities.SaveToFile(directory, name, Values);
        }

        public override void LoadFromFile(string directory, string name)
        {
            FileSpanUtilities.LoadFromFile(directory, name, Values);
        }
    }
}
