namespace TemporalStasis;

/// <summary>
/// Configuration for an <see cref="ILobbyProxy"/> instance, used to proxy connections to the zone server.
/// </summary>
/// <remarks>This does not need to be set unless the default config is outdated.</remarks>
public sealed class LobbyProxyConfig {
    /// <summary>The opcode used for the EnterWorld packet.</summary>
    public uint EnterWorldOpcode { get; set; } = 15;

    /// <summary>The offset in the EnterWorld packet that contains the zone server's port.</summary>
    public int EnterWorldPortOffset { get; set; } = 94;

    /// <summary>The offset in the EnterWorld packet that contains the zone server's address.</summary>
    public int EnterWorldHostOffset { get; set; } = 96;

    /// <summary>The size of the zone server's address in the EnterWorld packet.</summary>
    public int EnterWorldHostSize { get; set; } = 48;


    /// <summary>The version used in the encryption key.</summary>
    public uint EncryptionKeyVersion { get; set; } = 7201;

    /// <summary>The offset in the EncryptionInit segment that contains the key.</summary>
    public int EncryptionInitKeyOffset { get; set; } = 100;

    /// <summary>The offset in the EncryptionInit segment that contains the phrase.</summary>
    public int EncryptionInitPhraseOffset { get; set; } = 36;

    /// <summary>The size of the phrase in the EncryptionInit segment.</summary>
    public int EncryptionInitPhraseSize { get; set; } = 32;
}
