// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using BuildXL.Utilities.Configuration;

namespace BuildXL.Execution.Analyzers.PackedPipGraph
{
    /// <summary>
    /// Represents one-to-many relationship between two tables.
    /// </summary>
    /// <remarks>
    /// The RelationTable's state is three lists: 
    /// - Values: the per-id count of how many relationships each TFromId has.
    /// - m_offsets: the per-id index into m_relations for each TFromId; calculated by a scan over Values.
    /// - m_relations: the collection of all TToIds for all relationships; sorted by TFromId then TToId.
    /// 
    /// For example, if we have FromId 1 related to ToIds 1 and 2, and FromId 3 related to ToIds 1 and 3:
    /// Values: [2, 0, 2]
    /// m_offsets: [0, 2, 2]
    /// m_relations: [1, 2, 1, 3]
    /// </remarks>
    public class RelationTable<TFromId, TToId, TFromTable, TToTable> : DerivedTable<TFromId, int, TFromTable>
        where TFromId : unmanaged, Id<TFromId>
        where TToId : unmanaged, Id<TToId>
        where TFromTable : Table<TFromId>
        where TToTable : Table<TToId>
    {
        public readonly TToTable RelatedTable;

        /// <summary>
        /// List of offsets per TFromId.
        /// </summary>
        /// <remarks>
        /// Computed from a scan over Values, which is the per-TFromId count of relations per element.
        /// Doesn't need to be a SpannableList because we never save this, we recompute it on load.
        /// </remarks>
        private readonly List<int> m_offsets;

        /// <summary>
        /// List of actual relations.
        /// </summary>
        /// <remarks>
        /// Stored in order of TFromId, sorted within each group by TToId.
        /// </remarks>
        private readonly SpannableList<TToId> m_relations;

        private static readonly string s_relations = "Relations";

        public RelationTable(TFromTable baseTable, TToTable relatedTable) : base(baseTable)
        {
            RelatedTable = relatedTable;
            m_offsets = new List<int>(Count + 1);
            m_offsets.AddRange(Enumerable.Repeat<int>(0, Count + 1));
            m_relations = new SpannableList<TToId>();
        }

        public override void SaveToFile(string directory, string name)
        {
            base.SaveToFile(directory, name);
            FileSpanUtilities.SaveToFile(directory, $"{s_relations}.{name}", m_relations);
        }

        public override void LoadFromFile(string directory, string name)
        {
            base.LoadFromFile(directory, name);
            m_relations.Clear();
            FileSpanUtilities.LoadFromFile<TToId>(directory, $"{s_relations}.{name}", m_relations);
            CalculateOffsets();
        }

        private void CalculateOffsets()
        {
            int offset = 0;
            for (int i = 0; i < Count; i++)
            {
                m_offsets[i + 1] = offset;
                int count = this[default(TFromId).ToId(i)];
                offset += count;
            }
        }
        
        /// <summary>
        /// A span over the actual relations of this particular id.
        /// </summary>
        public ReadOnlySpan<TToId> Relations(TFromId id)
        {
            int offset = m_offsets[id.FromId()];
            int count = Values[id.FromId()];
            return m_relations.AsSpan().Slice(offset, count);
        }
    }
}
