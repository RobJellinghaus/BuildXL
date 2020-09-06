// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;

namespace BuildXL.Execution.Analyzers.PackedPipGraph
{
    /// <summary>
    /// Defines a new ID space and set of base values for each ID.
    /// </summary>
    /// <remarks>
    /// This serves as the "core data" of the entity with the given ID.
    /// Other derived tables and relations can be built sharing the same base table.
    /// 
    /// ID 0 is always preallocated and always has the default TValue; this lets us
    /// distinguish uninitialized IDs from allocated IDs, and gives us a default
    /// sentinel value in every table.
    /// </remarks>
    public abstract class ValueTable<TId, TValue> : Table<TId>
        where TId : struct, Id<TId>
    {
        /// <summary>
        /// List of values; index in list = ID of value.
        /// </summary>
        protected abstract IList<TValue> GetValues(); 

        public override int Count => GetValues().Count - 1;

        /// <summary>
        /// Return the current range of defined IDs.
        /// </summary>
        /// <remarks>
        /// Mainly useful for testing.
        /// </remarks>
        public override IEnumerable<TId> Ids =>
            GetValues().Count == 1 
                ? Enumerable.Empty<TId>() 
                : Enumerable.Range(1, GetValues().Count - 1).Select(v => default(TId).ToId(v));

        /// <summary>
        /// Get a value from the table.
        /// </summary>
        public virtual TValue this[TId id] => GetValues()[id.FromId()];

        /// <summary>
        /// Build a BaseTable, by creating a dictionary of items already added.
        /// </summary>
        public class CachingBuilder<TValueComparer>
            where TValueComparer : IEqualityComparer<TValue>, new()
        {
            /// <summary>
            /// Efficient lookup by hash value.
            /// </summary>
            /// <remarks>
            /// This is really only necessary when building the table, and should probably be split out into a builder type.
            /// </remarks>
            protected readonly Dictionary<TValue, TId> Entries = new Dictionary<TValue, TId>(new TValueComparer());

            protected readonly ValueTable<TId, TValue> ValueTable;

            internal CachingBuilder(ValueTable<TId, TValue> valueTable)
            {
                ValueTable = valueTable;
                // always skip the zero element
                IList<TValue> values = valueTable.GetValues();
                for (int i = 1; i < values.Count; i++)
                {
                    Entries.Add(values[i], default(TId).ToId(i));
                }
            }

            public virtual TId GetOrAdd(TValue value)
            {
                if (Entries.TryGetValue(value, out TId id))
                {
                    return id;
                }
                else
                {
                    // bit of an odd creation idiom, but should be zero-allocation and no-virtcall
                    id = default(TId).ToId(ValueTable.GetValues().Count);
                    ValueTable.GetValues().Add(value);
                    Entries.Add(value, id);
                    return id;
                }
            }
        }
    }
}
