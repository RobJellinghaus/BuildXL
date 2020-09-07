// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using BuildXL.ToolSupport;
using Microsoft.VisualStudio.Services.Location;

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
    /// Values: [0, 2, 0, 2]
    /// m_offsets: [0, 0, 2, 2]
    /// m_relations: [1, 2, 1, 3]
    /// 
    /// Note that Values and m_offsets both start with 0, as they map to IDs directly.
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
        /// When building incrementally, this list grows progressively; if this list has fewer elements
        /// than Count, it means only a prefix of all IDs have had their relations added yet.
        /// </remarks>
        private readonly SpannableList<int> m_offsets;

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
            m_offsets = new SpannableList<int>(Count + 1);
            m_offsets.Add(0);
            m_relations = new SpannableList<TToId>();

            Console.WriteLine($"RelationTable() end: {ToFullString()}");
        }

        public override void SaveToFile(string directory, string name)
        {
            base.SaveToFile(directory, name);
            FileSpanUtilities.SaveToFile(directory, $"{s_relations}.{name}", m_relations);
        }

        public override void LoadFromFile(string directory, string name)
        {
            base.LoadFromFile(directory, name);
            FileSpanUtilities.LoadFromFile<TToId>(directory, $"{s_relations}.{name}", m_relations);
            CalculateOffsets();
        }

        private void CalculateOffsets(int startIndex = 1)
        {
            for (int i = 1; i < Count; i++)
            {
                m_offsets[i] = m_offsets[i - 1] + Values[i - 1];
            }
        }
        
        /// <summary>
        /// A span over the actual relations of this particular id.
        /// </summary>
        public ReadOnlySpan<TToId> GetRelations(TFromId id)
        {
            int offset = m_offsets[id.FromId()];
            int count = Values[id.FromId()];
            return m_relations.AsSpan().Slice(offset, count);
        }

        /// <summary>
        /// Add relations in sequence; must always add the next id.
        /// </summary>
        /// <remarks>
        /// This supports only a very rudimentary form of appending, where we always append the
        /// full set of relations (possibly empty) of a subsequent ID in the base table.
        /// TODO: extend this to support skipping IDs without having to call for every non-related ID.
        /// </remarks>
        public void AddRelations(TFromId id, ReadOnlySpan<TToId> newRelations)
        {
            Console.WriteLine($"RelationTable.AddRelations start: {ToFullString()}");

            // Ensure newRelations are sorted.
            for (int i = 1; i < newRelations.Length; i++)
            {
                int previous = newRelations[i - 1].FromId();
                int current = newRelations[i].FromId();
                if (previous >= current)
                {
                    throw new ArgumentException($"Cannot add unsorted data to RelationTable: data[{i - 1}] = {previous}; data[{i}] = {current}");
                }
            }

            int idInt = id.FromId();
            if (idInt == m_offsets.Count)
            {
                // this is the only valid value at which relations can be added.
                m_offsets.Add(m_offsets[idInt - 1] + Values[idInt - 1]);
                Values[idInt] = newRelations.Length;

                m_relations.AddRange(newRelations);
            }
            else
            {
                throw new InvalidArgumentException(
                    $"Cannot add relations for {idInt} to backing list with m_offsets.Count {m_offsets.Count}, Values.Count {Values.Count}, m_relations.Count {m_relations.Count}");
            }

            Console.WriteLine($"RelationTable.AddRelations end: {ToFullString()}");
        }

        public string ToFullString() => $"Values {Values.ToFullString()}, m_offsets {m_offsets.ToFullString()}, m_relations {m_relations.ToFullString()}";

        /// <summary>
        /// Create a new RelationTable that is the inverse of this relation.
        /// </summary>
        public RelationTable<TToId, TFromId, TToTable, TFromTable> Invert()
        {
            RelationTable<TToId, TFromId, TToTable, TFromTable> result =
                new RelationTable<TToId, TFromId, TToTable, TFromTable>(RelatedTable, BaseTable);

            // We will use result.Values to accumulate the counts.
            result.Values.Fill(RelatedTable.Count, default);

            int sum = 0;
            foreach (TFromId id in BaseTable.Ids)
            {
                foreach (TToId relatedId in GetRelations(id))
                {
                    result.Values[relatedId.FromId()]++;
                    sum++;
                    Console.WriteLine($"RelationTable.Invert: TFromId {id}, TToId {relatedId}: {result.ToFullString()}");
                }
            }

            // Now we can calculate m_offsets.
            result.CalculateOffsets();

            Console.WriteLine($"RelationTable.Invert: after CalculateOffsets: {result.ToFullString()}");

            // And we know the necessary size of m_relations.
            result.m_relations.Capacity = sum;

            // Allocate an array of positions to track how many relations we have filled in.
            SpannableList<int> positions = new SpannableList<int>(RelatedTable.Count);
            positions.Fill(RelatedTable.Count, 0);

            // And accumulate all the inverse relations.
            foreach (TFromId id in BaseTable.Ids)
            {
                foreach (TToId relatedId in GetRelations(id))
                {
                    int relatedIdInt = relatedId.FromId();
                    int idInt = id.FromId();
                    int relationIndex = m_offsets[relatedIdInt] + positions[relatedIdInt];
                    result.m_relations[relationIndex] = id;
                    positions[relatedIdInt]++;
                    if (positions[relatedIdInt] > result.Values[relatedIdInt])
                    {
                        throw new Exception(
                            $"RelationTable.Inverse: logic exception: positions[{relatedIdInt}] = {positions[relatedIdInt]}, result.Values[{relatedIdInt}] = {result.Values[relatedIdInt]}");
                    }

                    Console.WriteLine($"RelationTable.Invert: TFromId {id}, TToId {relatedId}: {result.ToFullString()}");
                }
            }

            return result;
        }
    }
}
