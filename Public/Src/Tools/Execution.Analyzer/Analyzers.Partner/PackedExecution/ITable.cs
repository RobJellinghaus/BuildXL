// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace BuildXL.Execution.Analyzers.PackedExecution
{
    /// <summary>
    /// An ITable defines an ID space for its elements.
    /// </summary>
    /// <remarks>
    /// Derived ITable interfaces allow getting at one (ISingleValueTable)
    /// or multiple (IMultipleValueTable) values per ID.
    /// </remarks>
    public interface ITable<TId>
        where TId : unmanaged, Id<TId>
    {
        /// <summary>
        /// The IDs stored in this Table.
        /// </summary>
        IEnumerable<TId> Ids { get; }

        /// <summary>
        /// The number of IDs currently stored in the Table.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Is this ID valid in this table?
        /// </summary>
        bool IsValid(TId id);

        /// <summary>
        /// Check that the ID is valid, throwing an exception if not.
        /// </summary>
        void CheckValid(TId id);

        /// <summary>
        /// Save the contents of this table in the given directory with the given filename.
        /// </summary>
        void SaveToFile(string directory, string name);

        /// <summary>
        /// Load the contents of this table from the given directory with the given filename.
        /// </summary>
        /// <remarks>
        /// Any existing contents of this table will be discarded before loading.
        /// </remarks>
        void LoadFromFile(string directory, string name);

        /// <summary>
        /// The base table for this table, if any.
        /// </summary>
        /// <remarks>
        /// The base table, if it exists, defines the ID space for this table; this table is
        /// conceptually a "derived column" (or additional relation) over the IDs of the base
        /// table.
        /// </remarks>
        ITable<TId> BaseTableOpt { get; }
    }
}
