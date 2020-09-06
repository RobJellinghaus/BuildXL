// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace BuildXL.Execution.Analyzers.PackedPipGraph
{
    /// <summary>
    /// Defines a new ID space and set of base values for each ID, where the values are unmanaged.
    /// </summary>
    /// <remarks>
    /// This serves as the "core data" of the entity with the given ID.
    /// Other derived tables and relations can be built sharing the same base table.
    /// 
    /// ID 0 is always preallocated and always has the default TValue; this lets us
    /// distinguish uninitialized IDs from allocated IDs, and gives us a default
    /// sentinel value in every table.
    /// </remarks>
    public abstract class BaseUnmanagedTable<TId, TValue> : ValueTable<TId, TValue>
        where TId : struct, Id<TId>
        where TValue : unmanaged
    {
        /// <summary>
        /// List of values; index in list = ID of value.
        /// </summary>
        protected readonly SpannableList<TValue> Values; 

        public BaseUnmanagedTable(int capacity = DefaultCapacity)
        {
            if (capacity <= 0) { throw new ArgumentException($"Capacity {capacity} must be > 0"); }
            Values = new SpannableList<TValue>(capacity + 1);
            // The 0'th entry is always preallocated, so we can use it as a sentinel in any table.
            // In other words: 0 is never a valid backing value for any TId.
            Values.Add(default);
        }

        protected override IList<TValue> GetValues() => Values;

        public override void SaveToFile(string directory, string name)
        {
            FileSpanUtilities.SaveToFile<TValue>(directory, name, Values);
        }

        public override void LoadFromFile(string directory, string name)
        {
            FileSpanUtilities.LoadFromFile(directory, name, Values);
        }
    }
}
