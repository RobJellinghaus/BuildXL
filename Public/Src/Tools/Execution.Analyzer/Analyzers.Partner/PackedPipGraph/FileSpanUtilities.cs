// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using BuildXL.Utilities.Configuration.Mutable;

namespace BuildXL.Execution.Analyzers.PackedPipGraph
{
    public static class FileSpanUtilities
    {
        public static void SaveToFile<TValue>(string directory, string name, List<TValue> values)
            where TValue : unmanaged
        {
            using (Stream writer = File.OpenWrite(Path.Combine(directory, name)))
            {
                // kind of a lot of work to write out an int, but whatevs
                int[] length = new int[] { values.Count };
                writer.Write(MemoryMarshal.Cast<int, byte>(new Span<int>(length)));

                // screw it, do it the allocation-horrible way.
                // when .NET 5 gets here, CollectionsMarshal.AsSpan<T>(List<T>) will save us:
                // https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.collectionsmarshal.asspan?view=net-5.0#System_Runtime_InteropServices_CollectionsMarshal_AsSpan__1_System_Collections_Generic_List___0__
                TValue[] valueArray = values.ToArray();
                Span<TValue> valueSpan = new Span<TValue>(valueArray);
                Span<byte> byteValueSpan = MemoryMarshal.Cast<TValue, byte>(valueSpan);
                writer.Write(byteValueSpan);
            }
        }

        public static TValue[] LoadFromFile<TValue>(string directory, string name)
            where TValue : unmanaged
        {
            using (Stream reader = File.OpenRead(Path.Combine(directory, name)))
            {
                int[] length = new int[1];
                reader.Read(MemoryMarshal.Cast<int, byte>(new Span<int>(length)));

                // screw it, do it the allocation-horrible way.
                // when .NET 5 gets here, CollectionsMarshal.AsSpan<T>(List<T>) will save us:
                // https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.collectionsmarshal.asspan?view=net-5.0#System_Runtime_InteropServices_CollectionsMarshal_AsSpan__1_System_Collections_Generic_List___0__
                TValue[] values = new TValue[length[0]];
                Span<TValue> valueSpan = new Span<TValue>(values);
                Span<byte> byteValueSpan = MemoryMarshal.Cast<TValue, byte>(valueSpan);
                reader.Read(byteValueSpan);

                return values;
            }
        }
    }
}
