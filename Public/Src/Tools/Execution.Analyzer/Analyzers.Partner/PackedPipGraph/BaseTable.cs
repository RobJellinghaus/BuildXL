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
    /// </remarks>
    public abstract class BaseTable<TId, TValue> : Table<TId>
        where TId : struct, Id<TId>
    {
        /// <summary>
        /// List of values; index in list = ID of value.
        /// </summary>
        private readonly List<TValue> m_values = new List<TValue>(); 

        public BaseTable()
        {
            // The 0'th entry is always preallocated, so we can use it as a sentinel in any table.
            // In other words: 0 is never a valid backing value for any TId.
            m_values.Add(default(TValue));
        }

        /// <summary>
        /// Build a BaseTable, by creating a dictionary of items already added.
        /// </summary>
        public class CachingBuilder
        {
            /// <summary>
            /// Efficient lookup by string value.
            /// </summary>
            /// <remarks>
            /// This is really only necessary when building the table, and should probably be split out into a builder type.
            /// </remarks>
            private readonly Dictionary<TValue, TId> m_entries = new Dictionary<TValue, TId>();

            private readonly BaseTable<TId, TValue> m_baseTable;

            internal CachingBuilder(BaseTable<TId, TValue> baseTable)
            {
                m_baseTable = baseTable;
                // always skip the zero element
                for (int i = 1; i < baseTable.m_values.Count; i++)
                {
                    m_entries.Add(baseTable.m_values[i], default(TId).ToId(i));
                }
            }

            public TId GetOrAdd(TValue value)
            {
                if (m_entries.TryGetValue(value, out TId id))
                {
                    return id;
                }
                else
                {
                    // bit of an odd creation idiom, but should be zero-allocation and no-virtcall
                    id = default(TId).ToId(m_baseTable.m_values.Count);
                    m_baseTable.m_values.Add(value);
                    m_entries.Add(value, id);
                    return id;
                }
            }
        }

        public override IEnumerable<TId> Ids => 
            Enumerable.Range(1, m_values.Count).Select(v => default(TId).ToId(v));

        public TValue this[TId id] => m_values[id.FromId()];
    }
}
