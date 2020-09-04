// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Security.Cryptography;

namespace BuildXL.Execution.Analyzers.DumpPackedGraph
{
    /// <summary>
    /// Maps individual strings to numeric IDs.
    /// </summary>
    /// <remarks>
    /// This serves as the "atom table" for one or more NameTables.
    /// 
    /// By design this is append-only, with no delete operation; the goal here is
    /// to support a space-efficient readonly-after-construction representation.
    /// </remarks>
    public class StringTable
    {
        /// <summary>
        /// List of strings; index in string = ID of string.
        /// </summary>
        private readonly List<string> m_strings = new List<string>(); 

        public StringTable()
        {
        }

        /// <summary>
        /// Wrapper struct to prevent confusing StringTable IDs with other IDs.
        /// </summary>
        public struct Id
        {
            internal readonly int Value;
            internal Id(int value) { Value = value; }
        }

        /// <summary>
        /// Wrapper struct for passing around Ids that know their table.
        /// </summary>
        public struct Value
        {
            public readonly Id Id;
            public readonly StringTable StringTable;
            internal Value(Id id, StringTable stringTable) { Id = id; StringTable = stringTable; }
            public string String => StringTable.m_strings[Id.Value];
        }

        /// <summary>
        /// Build a StringTable, by caching strings for lookup while building.
        /// </summary>
        public class Builder
        {
            /// <summary>
            /// Efficient lookup by string value.
            /// </summary>
            /// <remarks>
            /// This is really only necessary when building the table, and should probably be split out into a builder type.
            /// </remarks>
            private readonly Dictionary<string, Id> m_entries = new Dictionary<string, Id>();

            private readonly StringTable m_stringTable;

            internal Builder(StringTable stringTable)
            {
                m_stringTable = stringTable;
                for (int i = 0; i < stringTable.m_strings.Count; i++)
                {
                    m_entries.Add(stringTable.m_strings[i], new Id(i));
                }
            }

            public Id GetOrAdd(string value)
            {
                if (m_entries.TryGetValue(value, out Id id))
                {
                    return id;
                }
                else
                {
                    id = new Id(m_stringTable.m_strings.Count);
                    m_stringTable.m_strings.Add(value);
                    m_entries.Add(value, id);
                    return id;
                }
            }
        }

        public Value Get(Id id) => new Value(id, this);
    }
}
