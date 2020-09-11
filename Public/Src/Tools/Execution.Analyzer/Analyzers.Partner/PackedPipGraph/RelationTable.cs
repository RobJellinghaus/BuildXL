// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using BuildXL.ToolSupport;

namespace BuildXL.Execution.Analyzers.PackedPipGraph
{
    /// <summary>
    /// Represents one-to-many relationship between two tables.
    /// </summary>
    /// <remarks>
    /// The RelationTable is a MultiValueTable which stores relations (TToIds) between the TFromTable and the TToTable.
    /// </remarks>
    public class RelationTable<TFromId, TToId, TFromTable, TToTable> : MultiValueTable<TFromId, TToId>
        where TFromId : unmanaged, Id<TFromId>
        where TToId : unmanaged, Id<TToId>
        where TFromTable : ITable<TFromId>
        where TToTable : ITable<TToId>
    {
        public readonly TToTable RelatedTable;

        /// <summary>
        /// Construct a RelationTable between baseTable and relatedTable.
        /// </summary>
        /// <remarks>
        /// This currently must be constructed after baseTable has been fully populated,
        /// or this table will not be able to preallocate its id table properly.
        /// TODO: remove this restriction.
        /// </remarks>
        public RelationTable(TFromTable baseTable, TToTable relatedTable) : base(baseTable)
        {
            RelatedTable = relatedTable;
        }

        public override ReadOnlySpan<TToId> this[TFromId id]
        { 
            get => base[id];
            set
            {
                CheckRelatedIds(value);
                base[id] = value;
            }
        }

        private void CheckRelatedIds(ReadOnlySpan<TToId> ids)
        {
            foreach (TToId id in ids)
            {
                RelatedTable.CheckValid(id);
            }
        }

        /// <summary>
        /// Add relations in sequence; must always add the next id.
        /// </summary>
        /// <remarks>
        /// This supports only a very rudimentary form of appending, where we always append the
        /// full set of relations (possibly empty) of a subsequent ID in the base table.
        /// TODO: extend this to support skipping IDs without having to call for every non-related ID.
        /// </remarks>
        public override TFromId Add(ReadOnlySpan<TToId> newRelations)
        {
            CheckRelatedIds(newRelations);
            // Ensure newRelations are sorted.
            for (int i = 1; i < newRelations.Length; i++)
            {
                int previous = newRelations[i - 1].FromId();
                int current = newRelations[i].FromId();
                if (previous >= current)
                {
                    throw new ArgumentException($"Cannot add unsorted and/or duplicate data to RelationTable: data[{i - 1}] = {previous}; data[{i}] = {current}");
                }
            }

            return base.Add(newRelations);
        }

        /// <summary>
        /// Create a new RelationTable that is the inverse of this relation.
        /// </summary>
        public RelationTable<TToId, TFromId, TToTable, TFromTable> Invert()
        {
            RelationTable<TToId, TFromId, TToTable, TFromTable> result =
                new RelationTable<TToId, TFromId, TToTable, TFromTable>(RelatedTable, (TFromTable)BaseTableOpt);

            // We will use result.Values to accumulate the counts as usual.
            result.SingleValues.Fill(RelatedTable.Count, 0);
            // And we will use result.m_offsets to store the offsets as usual.
            result.Offsets.Fill(RelatedTable.Count, 0);

            int sum = 0;
            foreach (TFromId id in BaseTableOpt.Ids)
            {
                foreach (TToId relatedId in this[id])
                {
                    result.SingleValues[relatedId.FromId() - 1]++;
                    sum++;
                }
            }

            // Now we can calculate m_offsets.
            result.CalculateOffsets();

            // And we know the necessary size of m_relations.
            result.MultiValues.Capacity = sum;
            result.MultiValues.Fill(sum, default);

            // Allocate an array of positions to track how many relations we have filled in.
            SpannableList<int> positions = new SpannableList<int>(RelatedTable.Count + 1);
            positions.Fill(RelatedTable.Count + 1, 0);

            // And accumulate all the inverse relations.
            foreach (TFromId id in BaseTableOpt.Ids)
            {
                foreach (TToId relatedId in this[id])
                {
                    int relatedIdInt = relatedId.FromId() - 1;
                    int idInt = id.FromId() - 1;
                    int offset = result.Offsets[relatedIdInt];
                    int position = positions[relatedIdInt];
                    int relationIndex = result.Offsets[relatedIdInt] + positions[relatedIdInt];
                    result.MultiValues[relationIndex] = id;
                    positions[relatedIdInt]++;
                    if (positions[relatedIdInt] > result.SingleValues[relatedIdInt])
                    {
                        throw new Exception(
                            $"RelationTable.Inverse: logic exception: positions[{relatedIdInt}] = {positions[relatedIdInt]}, result.SingleValues[{relatedIdInt}] = {result.SingleValues[relatedIdInt]}");
                    }
                    else if (positions[relatedIdInt] == result.SingleValues[relatedIdInt])
                    {
                        // all the relations for this ID are known. now, we have to sort them.
                        Span<TFromId> finalSpan = result.MultiValues.AsSpan().Slice(result.Offsets[relatedIdInt], result.SingleValues[relatedIdInt]);
                        SpanUtilities.Sort(finalSpan, (id1, id2) => id1.FromId().CompareTo(id2.FromId()));
                    }
                }
            }

            // TODO: error check that there are no zero entries in m_relations

            return result;
        }
    }
}
