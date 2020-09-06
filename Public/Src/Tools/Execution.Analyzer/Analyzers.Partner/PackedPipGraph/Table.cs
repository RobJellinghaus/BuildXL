// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace BuildXL.Execution.Analyzers.PackedPipGraph
{
    /// <summary>
    /// Interface implemented by ID types, to enable conversion to and from int.
    /// </summary>
    /// <remarks>
    /// Theoretically, thanks to generic instantiation, these methods should be callable with zero overhead
    /// (no boxing, no virtual calls).
    /// </remarks>
    public interface Id<TId>
        where TId : struct
    {
        internal int FromId();
        internal TId ToId(int value);
    }

    /// <summary>
    /// An abstract table defines an ID space for its elements.
    /// </summary>
    /// <remarks>
    /// </remarks>
    public abstract class Table<TId>
    {
        public Table()
        {
        }

        /// <summary>
        /// The IDs stored in this Table.
        /// </summary>
        public abstract IEnumerable<TId> Ids { get; }

        /// <summary>
        /// The number of IDs currently stored in the Table.
        /// </summary>
        public abstract int Count { get; }

        /// <summary>
        /// Save the contents of this table in the given directory with the given filename.
        /// </summary>
        public abstract void SaveToFile(string directory, string name);

        /// <summary>
        /// Load the contents of this table from the given directory with the given filename.
        /// </summary>
        /// <remarks>
        /// Any existing contents of this table will be discarded before loading.
        /// </remarks>
        public abstract void LoadFromFile(string directory, string name);
    }
}
