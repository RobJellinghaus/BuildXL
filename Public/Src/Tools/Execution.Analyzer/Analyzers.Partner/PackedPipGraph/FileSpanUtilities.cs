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
        public static void SaveToFile<TValue>(string directory, string name, SpannableList<TValue> values)
            where TValue : unmanaged
        {
            string path = Path.Combine(directory, name);
            Console.WriteLine($"FileSpanUtilities.SaveToFile: writing to {path} contents {values.ToFullString()}");

            using (Stream writer = File.OpenWrite(path))
            {
                // kind of a lot of work to write out an int, but whatevs
                int[] length = new int[] { values.Count };
                writer.Write(MemoryMarshal.Cast<int, byte>(new Span<int>(length)));

                writer.Write(MemoryMarshal.Cast<TValue, byte>(values.AsSpan()));
            }
        }

        public static void LoadFromFile<TValue>(string directory, string name, SpannableList<TValue> values)
            where TValue : unmanaged
        {
            values.Clear();

            string path = Path.Combine(directory, name);
            using (Stream reader = File.OpenRead(path))
            {
                int[] lengthBuf = new int[1];
                reader.Read(MemoryMarshal.Cast<int, byte>(new Span<int>(lengthBuf)));
                int length = lengthBuf[0];

                Console.WriteLine($"FileSpanUtilities.LoadFromFile: reading {length} values");

                values.Fill(length, default);

                Span<TValue> valueSpan = values.AsSpan();
                Span<byte> byteValueSpan = MemoryMarshal.Cast<TValue, byte>(valueSpan);
                reader.Read(byteValueSpan);
            }

            Console.WriteLine($"FileSpanUtilities.LoadFromFile: read from {path} contents {values.ToFullString()}");
        }
    }
}
