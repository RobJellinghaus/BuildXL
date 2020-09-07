// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace BuildXL.Execution.Analyzers.PackedPipGraph
{
    public static class SpanUtilities
    {
        // source: https://github.com/kevin-montrose/Cesil/blob/master/Cesil/Common/Utils.cs#L870
        //     via https://github.com/dotnet/runtime/issues/19969
        // todo: once MemoryExtensions.Sort() lands we can remove all of this (tracking issue: https://github.com/kevin-montrose/Cesil/issues/29)
        //       coming as part of .NET 5, as a consequence of https://github.com/dotnet/runtime/issues/19969
        public static void Sort<T>(Span<T> span, Comparison<T> comparer)
        {
            // crummy quick sort implementation, all of this should get killed

            var len = span.Length;

            if (len <= 1)
            {
                return;
            }

            if (len == 2)
            {
                var a = span[0];
                var b = span[1];

                var res = comparer(a, b);
                if (res > 0)
                {
                    span[0] = b;
                    span[1] = a;
                }

                return;
            }

            // we only ever call this when the span isn't _already_ sorted,
            //    so our sort can be really dumb
            // basically Lomuto (see: https://en.wikipedia.org/wiki/Quicksort#Lomuto_partition_scheme)

            var splitIx = Partition(span, comparer);

            var left = span[..splitIx];
            var right = span[(splitIx + 1)..];

            Sort(left, comparer);
            Sort(right, comparer);

            // re-order subSpan such that items before the returned index are less than the value
            //    at the returned index
            static int Partition(Span<T> subSpan, Comparison<T> comparer)
            {
                var len = subSpan.Length;

                var pivotIx = len - 1;
                var pivotItem = subSpan[pivotIx];

                var i = 0;

                for (var j = 0; j < len; j++)
                {
                    var item = subSpan[j];
                    var res = comparer(item, pivotItem);

                    if (res < 0)
                    {
                        Swap(subSpan, i, j);
                        i++;
                    }
                }

                Swap(subSpan, i, pivotIx);

                return i;
            }

            static void Swap(Span<T> subSpan, int i, int j)
            {
                var oldI = subSpan[i];
                subSpan[i] = subSpan[j];
                subSpan[j] = oldI;
            }
        }
    }
}
