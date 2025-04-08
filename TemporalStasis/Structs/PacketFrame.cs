﻿namespace TemporalStasis.Structs;

/// <summary>A packet frame. This is the main container of FFXIV packets.</summary>
public ref struct PacketFrame {
    public ref FrameHeader FrameHeader;
    public Memory<byte> Data;
}
