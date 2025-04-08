using System.Runtime.InteropServices;
using static TemporalStasis.Compression.OodleLibraryFactory;

namespace TemporalStasis.Compression;

internal partial class OodleLibraryTcp : IOodle {
    private const int HtBits = 17;
    private const int WindowSize = 0x100000;

    [LibraryImport(OodleLibraryName, EntryPoint = "OodleNetwork1TCP_State_Size")]
    private static partial int OodleStateSize();

    [LibraryImport(OodleLibraryName, EntryPoint = "OodleNetwork1_Shared_Size")]
    private static partial int OodleSharedSize(int htbits);

    [LibraryImport(OodleLibraryName, EntryPoint = "OodleNetwork1_Shared_SetWindow")]
    private static partial void OodleSharedSetWindow(
        Span<byte> data, int htbits, Span<byte> window, int windowSize
    );

    [LibraryImport(OodleLibraryName, EntryPoint = "OodleNetwork1TCP_Train")]
    private static partial void OodleTcpTrain(
        Span<byte> state, Span<byte> shared,
        nint pointers, nint sizes, int numPackets
    );

    [LibraryImport(OodleLibraryName, EntryPoint = "OodleNetwork1TCP_Decode")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool OodleTcpDecode(
        Span<byte> state, Span<byte> shared,
        ReadOnlySpan<byte> compressed, int compressedSize,
        Span<byte> raw, int rawSize
    );

    [LibraryImport(OodleLibraryName, EntryPoint = "OodleNetwork1TCP_Encode")]
    public static partial int OodleTcpEncode(
        Span<byte> state, Span<byte> shared, ReadOnlySpan<byte> raw, int rawSize, Span<byte> compressed
    );

    private readonly Lock @lock = new();

    private readonly int stateSize;
    private readonly int sharedSize;
    private readonly byte[] state;
    private readonly byte[] shared;
    private readonly byte[] window = new byte[WindowSize];

    public OodleLibraryTcp() {
        this.stateSize = OodleStateSize();
        this.state = new byte[this.stateSize];

        this.sharedSize = OodleSharedSize(HtBits);
        this.shared = new byte[this.sharedSize];

        OodleSharedSetWindow(this.shared, HtBits, this.window, WindowSize);
        OodleTcpTrain(this.state, this.shared, 0, 0, 0);
    }

    public int Compress(Span<byte> input, Span<byte> output) {
        lock (this.@lock) {
            return OodleTcpEncode(this.state, this.shared, input, input.Length, output);
        }
    }

    public void Decompress(Span<byte> input, Span<byte> output, int decompressedSize) {
        lock (this.@lock) {
            var result = OodleTcpDecode(this.state, this.shared, input, input.Length, output, decompressedSize);
            if (!result) throw new Exception("Failed to decompress data with Oodle");
        }
    }

    public void Dispose() { }
}
