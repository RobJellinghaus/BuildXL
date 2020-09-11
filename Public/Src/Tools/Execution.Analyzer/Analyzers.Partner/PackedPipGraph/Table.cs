// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using BuildXL.ToolSupport;

namespace BuildXL.Execution.Analyzers.PackedPipGraph
{
    /// <summary>
    /// Base abstract Table class with some common implementation.
    /// </summary>
    public abstract class Table<TId, TValue> : ITable<TId>
        where TId : unmanaged, Id<TId>
        where TValue : unmanaged
    {
        protected const int DefaultCapacity = 100;

        public Table(int capacity = -1)
        {
            SingleValues = new SpannableList<TValue>(capacity == -1 ? DefaultCapacity : capacity);
        }

        public Table(ITable<TId> baseTable)
        {
            if (baseTable == null) { throw new ArgumentException("Base table must not be null"); }
            BaseTableOpt = baseTable;
            SingleValues = new SpannableList<TValue>(baseTable.Count);
        }

        /// <summary>
        /// List of values; index in list + 1 = ID of value.
        /// </summary>
        protected SpannableList<TValue> SingleValues;

        /// <summary>
        /// If this table has a BaseTableOpt, its Count and its IDs are determined by that base table,
        /// and this table effectively has default / empty values for all IDs of its base table.
        /// </summary>
        public int Count => BaseTableOpt?.Count ?? SingleValues.Count;

        /// <summary>
        /// Return the current range of defined IDs.
        /// </summary>
        /// <remarks>
        /// Mainly useful for testing.
        /// </remarks>
        public IEnumerable<TId> Ids => Enumerable.Range(1, Count).Select(v => default(TId).ToId(v));

        /// <summary>
        /// The base table (if any) that defines this table's ID range.
        /// </summary>
        /// <remarks>
        /// "Opt" signifies "optional".
        /// </remarks>
        public ITable<TId> BaseTableOpt { get; private set; }

        public bool IsValid(TId id)
        {
            return id.FromId() > 0 && id.FromId() <= Count;
        }

        public void CheckValid(TId id)
        {
            if (!IsValid(id)) { throw new ArgumentException($"ID {id} is not valid for table with Count {Count}"); }
        }

        /// <summary>
        /// Ensure this table stores at least as many elements as the base table.
        /// </summary>
        protected void EnsureCount()
        {
            if (BaseTableOpt != null)
            {
                if (Count > SingleValues.Count)
                {
                    SingleValues.AddRange(Enumerable.Repeat<TValue>(default, BaseTableOpt.Count - SingleValues.Count));
                }
            }
        }

        public virtual void SaveToFile(string directory, string name)
        {
            FileSpanUtilities.SaveToFile(directory, name, SingleValues);
        }

        public virtual void LoadFromFile(string directory, string name)
        {
            FileSpanUtilities.SaveToFile(directory, name, SingleValues);
        }

        /// <summary>
        /// Add the given suffix to the filename, preserving extension.
        /// </summary>
        /// <remarks>
        /// For example, if this table is a MultiValuesTable and path is "foo.bin",
        /// the derived implementation may call this with "foo.bin" and "MultiValues",
        /// and will get "foo.MultiValues.bin" back.
        /// </remarks>
        protected string InsertSuffix(string path, string suffix)
        {
            int lastDot = path.LastIndexOf('.');
            return $"{path.Substring(0, lastDot)}.{suffix}{path.Substring(lastDot)}";
        }
    }
}
