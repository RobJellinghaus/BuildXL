// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace BuildXL.Execution.Analyzers.PackedExecution
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
    public class SingleValueTable<TId, TValue> : Table<TId, TValue>, ISingleValueTable<TId, TValue>
        where TId : unmanaged, Id<TId>
        where TValue : unmanaged
    {
        public SingleValueTable(int capacity = DefaultCapacity) : base(capacity)
        { }

        public SingleValueTable(ITable<TId> baseTable) : base(baseTable)
        { }

        /// <summary>
        /// Get a value from the table.
        /// </summary>
        public TValue this[TId id]
        {
            get
            {
                CheckValid(id);
                return SingleValues[id.FromId() - 1];
            }
            set
            {
                CheckValid(id);
                SingleValues[id.FromId() - 1] = value;
            }
        }

        /// <summary>
        /// Add the next value in the table.
        /// </summary>
        public TId Add(TValue value)
        {
            SingleValues.Add(value);
            return default(TId).ToId(Count);
        }

        /// <summary>
        /// Build a SingleValueTable which caches items by hash value, adding any item only once.
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

            protected readonly SingleValueTable<TId, TValue> ValueTable;

            internal CachingBuilder(SingleValueTable<TId, TValue> valueTable)
            {
                ValueTable = valueTable;
                // Prepopulate the dictionary that does the caching
                for (int i = 0; i < ValueTable.Count; i++)
                {
                    TId id = default(TId).ToId(i + 1);
                    Entries.Add(ValueTable[id], id);
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
                    id = ValueTable.Add(value);
                    Entries.Add(value, id);
                    return id;
                }
            }
        }
    }
}
