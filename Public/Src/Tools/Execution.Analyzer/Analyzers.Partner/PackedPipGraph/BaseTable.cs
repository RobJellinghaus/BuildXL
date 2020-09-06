// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace BuildXL.Execution.Analyzers.PackedPipGraph
{
    /// <summary>
    /// Defines a new ID space and set of base values for each ID, where the values may be managed.
    /// </summary>
    /// <remarks>
    /// This serves as the "core data" of the entity with the given ID.
    /// Other derived tables and relations can be built sharing the same base table.
    /// </remarks>
    public abstract class BaseTable<TId, TValue> : ValueTable<TId, TValue>
        where TId : struct, Id<TId>
    {
        protected List<TValue> Values;

        public BaseTable(int capacity = DefaultCapacity)
        {
            if (capacity <= 0) { throw new ArgumentException($"Capacity {capacity} must be > 0"); }
            Values = new List<TValue>(capacity);
        }

        protected override IList<TValue> GetValues() => Values;
    }
}
