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
    public abstract class ValueTable<TId, TValue> : Table<TId>
        where TId : struct, Id<TId>
    {
        /// <summary>
        /// List of values; index in list = ID of value.
        /// </summary>
        protected readonly List<TValue> Values = new List<TValue>(); 

        public ValueTable()
        {
            // The 0'th entry is always preallocated, so we can use it as a sentinel in any table.
            // In other words: 0 is never a valid backing value for any TId.
            Values.Add(default(TValue));
        }

        public int Count() => Values.Count - 1;

        /// <summary>
        /// Return the current range of defined IDs.
        /// </summary>
        /// <remarks>
        /// Mainly useful for testing.
        /// </remarks>
        public override IEnumerable<TId> Ids =>
            Values.Count == 1 
                ? Enumerable.Empty<TId>() 
                : Enumerable.Range(1, Values.Count - 1).Select(v => default(TId).ToId(v));

        /// <summary>
        /// Get a value from the table.
        /// </summary>
        public TValue this[TId id] => Values[id.FromId()];
    }
}
