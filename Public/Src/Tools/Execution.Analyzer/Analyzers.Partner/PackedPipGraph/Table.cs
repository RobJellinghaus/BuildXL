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

        public abstract IEnumerable<TId> Ids { get; }
    }
}
