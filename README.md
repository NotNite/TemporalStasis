# Temporal Stasis

Temporal Stasis is a FFXIV network proxy library.

Unlike traditional packet capturing tools, Temporal Stasis **stands between the game client and game server as a proxy**, decrypting/decompressing messages and repackaging them on the fly. Temporal Stasis can be used as a library to **record, drop, and even modify packets**. Because it's a proxy, Temporal Stasis **doesn't require modifying your game install at all**, and you don't have to inject into the game process to use Temporal Stasis.

Here's a simple comparison between other packet capturing tools:

| Project                  | Works without admin | Usable as a library     | Works without injection      | Supports packet modification |
|--------------------------|---------------------|-------------------------|------------------------------|------------------------------|
| Temporal Stasis          | :white_check_mark:  | :white_check_mark:      | :white_check_mark:           | :white_check_mark:           |
| [Deucalion][deucalion]   | :white_check_mark:  | :white_check_mark:      | :x:                          | :x:                          |
| [Chronofoil][chronofoil] | :white_check_mark:  | :x:                     | :x:                          | :x:                          |
| [Machina][machina]       | :warning:[^1]       | :white_check_mark:      | :warning:[^2]                | :x:                          |

[^1]: Machina requires [WinPcap](https://www.winpcap.org/) installed to work without administrative privileges.
[^2]: Machina versions v2.4.2.2 and above require Deucalion to deobfuscate some obfuscated packets.

[deucalion]: <https://github.com/ff14wed/deucalion>
[machina]: <https://github.com/ravahn/machina>
[chronofoil]: <https://github.com/ProjectChronofoil/Chronofoil.Plugin>

## Requirements

There are three "layers" of the FFXIV protocol that must be handled: encryption (in the lobby server), compression (in the zone server), and obfuscation (in some zone server packets). There's also some extra things that change often (like packet opcodes).

- **Temporal Stasis requires the Oodle Network Compression library.** This must be present at runtime so Temporal Stasis can load it and decompress packets.
  - Libraries for Oodle Network Compression can be obtained [here](https://github.com/WorkingRobot/OodleUE) (in the Releases tab). For Windows users, extract `oodle-network-shared.dll` from `msvc.zip` and place it next to your application executable.
  - In the future, Temporal Stasis may allow loading Oodle from the FFXIV game executable.
- **Temporal Stasis does not deobfuscate packets.** Note that obfuscation only applies to some fields of some packets in the zone server, and you might not need to worry about packet obfuscation, depending on your use case.
  - Deobfuscating packets is outside the scope of Temporal Stasis, as packet obfuscation changes every game update. You can combine Temporal Stasis with a library like [Unscrambler](https://github.com/perchbirdd/Unscrambler), if you need.
- **Temporal Stasis does not provide packet opcodes or structs.** IPC packet opcodes change every game update, and their fields shift often. If you are reading data out of specific packets, you are expected to know the current opcode and layout of the packet.
- Temporal Stasis automatically provides the offsets and opcodes required for the lobby server (specifically the `EncryptionInit` segment and the `EnterWorld` packet). If these values are out of date, you must provide the correct values (in the `LobbyProxyConfig` class) for Temporal Stasis to work correctly.

## Usage

Temporal Stasis is a library, and you're intended to use it in your own code. There's an example project in this repository (`TemporalStasis.Example`) that can show you around the API.

To connect to Temporal Stasis, you need to redirect the game client to connect to your proxy instead of the original lobby server, and then your proxy will then connect to the original lobby server. This can be done in a few ways:

- Pass arguments to the game client to override the default lobby server (`DEV.LobbyHost04=127.0.0.1 DEV.LobbyPort04=44994`)
- Set up a custom DNS server and override the lobby server DNS records (`neolobby02.ffxiv.com`)

Those arguments and that DNS record point to the Aether data center. Note that the index from the command line and name of the DNS record do not match up (if you need a reference, see the `WorldDCGroupType` sheet [here](https://v2.xivapi.com/api/sheet/WorldDCGroupType?fields=Name,NeolobbyId)).

The lobby proxy will rewrite some packets to make the game client connect to the zone proxy. From there, you can intercept packets in normal gameplay.
