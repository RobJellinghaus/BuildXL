﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Security.Cryptography;

namespace BuildXL.Execution.Analyzers.PackedPipGraph
{
    /// <summary>
    /// Boilerplate ID type to avoid ID confusion in code.
    /// </summary>
    public struct StringId : Id<StringId>
    {
        internal readonly int Value;
        internal StringId(int value) { Value = value; }
        int Id<StringId>.FromId() => Value;
        StringId Id<StringId>.ToId(int value) => new StringId(value);
    }

    /// <summary>
    /// Table of unique strings.
    /// </summary>
    /// <remarks>
    /// Yes, that's all there is to it :-) Use a StringTable.CachingBuilder to populate one of these.
    /// </remarks>
    public class StringTable : BaseTable<StringId, string>
    {
        public StringTable()
        {
        }
    }
}