using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TemporalStasis.Encryption;

// Adapted from Asriel's fork: https://github.com/WorkingRobot/TemporalStasis/blob/7445923435a3667090eadf0ef1fdf1efc532285f/TemporalStasis/Encryption/Brokefish.cs
internal partial class Brokefish {
    private const MethodImplOptions OptimizationFlags =
        MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization;

    private const int N = 16;
    private readonly uint[] p = new uint[16 + 2];
    private readonly uint[,] s = new uint[4, 256];

    public Brokefish(Span<byte> key) {
        Buffer.BlockCopy(PValues, 0, this.p, 0, PValues.Length);
        Buffer.BlockCopy(SValues, 0, this.s, 0, SValues.Length);

        var j = 0;
        for (var i = 0; i < N + 2; ++i) {
            var data = 0;

            for (var k = 0; k < 4; ++k) {
#pragma warning disable CS0675 // Not my bug, it's Square Enix's
                data = (data << 8) | (sbyte) key[j];
#pragma warning restore CS0675
                j = (short) (j + 1);
                if (j >= key.Length) j = 0;
            }

            this.p[i] ^= (uint) data;
        }

        var datal = 0x00000000u;
        var datar = 0x00000000u;

        for (var i = 0; i < N + 2; i += 2) {
            this.BlowfishEncipher(ref datal, ref datar);
            this.p[i] = datal;
            this.p[i + 1] = datar;
        }

        for (var i = 0; i < 4; ++i) {
            for (j = 0; j < 256; j += 2) {
                this.BlowfishEncipher(ref datal, ref datar);
                this.s[i, j] = datal;
                this.s[i, j + 1] = datar;
            }
        }
    }


    [StructLayout(LayoutKind.Explicit, Pack = 8, Size = 8)]
    private struct Split64 {
        [FieldOffset(0)] public uint L;
        [FieldOffset(4)] public uint R;
    }

    [StructLayout(LayoutKind.Explicit, Pack = 4, Size = 4)]
    private readonly struct Split32(uint data) {
        [FieldOffset(0)] public readonly byte A;
        [FieldOffset(1)] public readonly byte B;
        [FieldOffset(2)] public readonly byte C;
        [FieldOffset(3)] public readonly byte D;
        [FieldOffset(0)] public readonly uint Data = data;
    }

    /// <summary>Enciphers unencrypted data in place, leaving extra data at the end untouched.</summary>
    public void EncipherPadded(Span<byte> data) => this.Encipher(data[..(data.Length & -32)]);

    /// <summary>Deciphers encrypted data in place, leaving extra data at the end untouched.</summary>
    public void DecipherPadded(Span<byte> data) => this.Decipher(data[..(data.Length & -32)]);

    /// <summary>Enciphers unencrypted data in place.</summary>
    public void Encipher(Span<byte> data) {
        foreach (ref var chunk in MemoryMarshal.Cast<byte, Split64>(data)) {
            this.BlowfishEncipher(ref chunk.L, ref chunk.R);
        }
    }

    /// <summary>Deciphers encrypted data in place.</summary>
    public void Decipher(Span<byte> data) {
        foreach (ref var chunk in MemoryMarshal.Cast<byte, Split64>(data)) {
            this.BlowfishDecipher(ref chunk.L, ref chunk.R);
        }
    }

    [MethodImpl(OptimizationFlags)]
    private uint F(uint x) {
        var splitX = new Split32(x);
        return ((this.s[0, splitX.D] + this.s[1, splitX.C]) ^ (this.s[2, splitX.B])) + this.s[3, splitX.A];
    }

    [MethodImpl(OptimizationFlags)]
    private void BlowfishEncipher(ref uint xl, ref uint xr) {
        for (var i = 0; i < N; ++i) {
            xl ^= this.p[i];
            xr = this.F(xl) ^ xr;
            (xl, xr) = (xr, xl);
        }

        (xl, xr) = (xr, xl);

        xr ^= this.p[N];
        xl ^= this.p[N + 1];
    }

    [MethodImpl(OptimizationFlags)]
    private void BlowfishDecipher(ref uint xl, ref uint xr) {
        for (var i = N + 1; i > 1; --i) {
            xl ^= this.p[i];
            xr = this.F(xl) ^ xr;

            (xl, xr) = (xr, xl);
        }

        (xl, xr) = (xr, xl);

        xl ^= this.p[0];
        xr ^= this.p[1];
    }
}
