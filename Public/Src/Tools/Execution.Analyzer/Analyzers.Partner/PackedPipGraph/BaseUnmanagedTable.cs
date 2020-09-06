// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace BuildXL.Execution.Analyzers.PackedPipGraph
{
    /// <summary>
    /// A BaseTable with values restricted to unmanaged types.
    /// </summary>
    /// <remarks>
    /// This lets us safely write generic span-based save and load methods for the contents.
    /// </remarks>
    public abstract class BaseUnmanagedTable<TId, TValue> : BaseTable<TId, TValue>
        where TId : struct, Id<TId>
        where TValue : unmanaged
    {
        public BaseUnmanagedTable(int capacity = -1) : base(capacity)
        {
        }

        public override void SaveToFile(string directory, string name)
        {
            FileSpanUtilities.SaveToFile<TValue>(directory, name, Values);
        }

        public override void LoadFromFile(string directory, string name)
        {
            Values.Clear();
            Values.AddRange(FileSpanUtilities.LoadFromFile<TValue>(directory, name));
        }
    }
}
