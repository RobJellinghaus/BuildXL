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
    public abstract class BaseTable<TId, TValue> : ValueTable<TId, TValue>
        where TId : struct, Id<TId>
    {
        public BaseTable()
        {
        }

        /// <summary>
        /// Build a BaseTable, by creating a dictionary of items already added.
        /// </summary>
        public class Builder
        {
            /// <summary>
            /// Efficient lookup by string value.
            /// </summary>
            /// <remarks>
            /// This is really only necessary when building the table, and should probably be split out into a builder type.
            /// </remarks>
            protected readonly Dictionary<TValue, TId> Entries = new Dictionary<TValue, TId>();

            protected readonly BaseTable<TId, TValue> BaseTable;

            internal Builder(BaseTable<TId, TValue> baseTable)
            {
                BaseTable = baseTable;
                // always skip the zero element
                for (int i = 1; i < baseTable.Values.Count; i++)
                {
                    Entries.Add(baseTable.Values[i], default(TId).ToId(i));
                }
            }

            public TId GetOrAdd(TValue value)
            {
                if (Entries.TryGetValue(value, out TId id))
                {
                    return id;
                }
                else
                {
                    // bit of an odd creation idiom, but should be zero-allocation and no-virtcall
                    id = default(TId).ToId(BaseTable.Values.Count);
                    BaseTable.Values.Add(value);
                    Entries.Add(value, id);
                    return id;
                }
            }
        }
    }
}
