namespace TemporalStasis.Compression;

public interface IOodle : IDisposable {
    /// <summary>Compresses decompressed data.</summary>
    /// <returns>The size of the compressed data.</returns>
    int Compress(Span<byte> input, Span<byte> output);

    /// <summary>Decompresses compressed data.</summary>
    void Decompress(Span<byte> input, Span<byte> output, int decompressedSize);
}
