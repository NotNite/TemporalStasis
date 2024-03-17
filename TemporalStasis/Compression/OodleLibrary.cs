using NativeLibraryLoader;

namespace TemporalStasis.Compression;

public unsafe class OodleLibrary : IOodle {
    public const int HtBits = 17;
    public const int WindowSize = 0x100000;
    
    public delegate int StateSizeDelegate();
    public delegate int SharedSizeDelegate(int htbits);
    public delegate void SharedSetWindowDelegate(byte[] data, int htbits, byte[] window, int windowSize);
    public delegate void TcpTrainDelegate(byte[] state, byte[] shared, nint pointers, nint sizes, int numPackets);
    public delegate bool TcpDecodeDelegate(byte[] state, byte[] shared, byte* compressed, int compressedSize, byte[] raw, int rawSize);
    public delegate int TcpEncodeDelegate(byte[] state, byte[] shared, byte[] raw, int rawSize, byte[] compressed);
    
    private NativeLibrary lib;
    private int stateSize;
    private int sharedSize;
    private byte[] state;
    private byte[] shared;
    private byte[] window = new byte[WindowSize];
    private TcpDecodeDelegate decode;
    private TcpEncodeDelegate encode;
    
    private object @lock = new();
    
    public OodleLibrary(string path) {
        this.lib = new NativeLibrary(path);
        
        var stateSizeFunc = lib.LoadFunction<StateSizeDelegate>("OodleNetwork1TCP_State_Size");
        this.stateSize = stateSizeFunc();
        
        var sharedSizeFunc = lib.LoadFunction<SharedSizeDelegate>("OodleNetwork1_Shared_Size");
        this.sharedSize = sharedSizeFunc(HtBits);
        
        this.state = new byte[this.stateSize];
        this.shared = new byte[this.sharedSize];
        
        var sharedSetWindowFunc = lib.LoadFunction<SharedSetWindowDelegate>("OodleNetwork1_Shared_SetWindow");
        sharedSetWindowFunc(this.shared, HtBits, this.window, WindowSize);
        
        var tcpTrainFunc = lib.LoadFunction<TcpTrainDelegate>("OodleNetwork1TCP_Train");
        tcpTrainFunc(this.state, this.shared, 0, 0, 0);
        
        this.decode = lib.LoadFunction<TcpDecodeDelegate>("OodleNetwork1TCP_Decode");
        this.encode = lib.LoadFunction<TcpEncodeDelegate>("OodleNetwork1TCP_Encode");
    }
    
    public byte[] Encode(byte[] input) {
        lock (this.@lock) {
            var output = new byte[input.Length];
            var len = this.encode(this.state, this.shared, input, input.Length, output);
            return output[..len];
        }
    }
    
    public byte[] Decode(byte[] input, int decompressedSize) {
        lock (this.@lock) {
            var output = new byte[decompressedSize];
            fixed (byte* ptr = input) {
                if (!this.decode(this.state, this.shared, ptr, input.Length, output, decompressedSize)) {
                    throw new Exception("Failed to decode Oodle packet");
                }
            }
            return output;
        }
    }
    
    public void Dispose() {
        this.lib.Dispose();
    }
}
