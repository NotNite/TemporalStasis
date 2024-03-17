namespace TemporalStasis.Structs;

public enum SegmentType : ushort {
    SessionInit = 1,
    Ipc = 3,
    KeepAlive = 7,
    EncryptionInit = 9
}
