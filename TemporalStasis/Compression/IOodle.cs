namespace TemporalStasis.Compression;

public interface IOodle : IDisposable {
    byte[] Encode(byte[] input);
    byte[] Decode(byte[] input, int decompressedSize);
}
