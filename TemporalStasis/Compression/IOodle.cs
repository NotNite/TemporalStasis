﻿namespace TemporalStasis.Compression;

public interface IOodle : IDisposable {
    /// <summary>Compresses decompressed data.</summary>
    /// <returns>The size of the compressed data.</returns>
    int Compress(ReadOnlySpan<byte> input, Span<byte> output);

    /// <summary>Decompresses compressed data.</summary>
    void Decompress(ReadOnlySpan<byte> input, Span<byte> output, int decompressedSize);
}
