// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net;

namespace BuildXL.Execution.Analyzers.PackedPipGraph
{
    /// <summary>
    /// Interface implemented by ID types, to enable generic conversion to and from int.
    /// </summary>
    /// <remarks>
    /// Theoretically, thanks to generic specialization, these methods should be callable with zero overhead
    /// (no boxing, no virtual calls).
    /// 
    /// Note that ID values are 1-based; IDs range from [1 .. Count] inclusive, rather than [0 .. Count-1] inclusive
    /// as with zero-based indices. This is deliberate, to allow the default ID value to indicate "no ID" (and
    /// to catch bugs relating to uninitialized IDs).
    /// </remarks>
    public interface Id<TId>
        where TId : unmanaged
    {
        /// <summary>
        /// Convert the ID to an integer value.
        /// </summary>
        /// <remarks>
        /// Note that the return value is still 1-based; to convert into an array index, you must subtract 1.
        /// </remarks>
        public int FromId();
        /// <summary>
        /// Convert an integer value to this type of ID.
        /// </summary>
        /// <remarks>
        /// This is a bit roundabout but is a generic way of constructing the appropriate kind of ID struct.
        /// Note that the value here must be 1-based; this method does not modify the value.
        /// </remarks>
        public TId ToId(int value);

        public static void CheckNotZero(int value) { if (value == 0) { throw new ArgumentException("Cannot create ID with value 0 (use default instead)"); } }
    }
}
