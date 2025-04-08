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

## Usage

Temporal Stasis is a library, and you're intended to use it in your own code. There's an example project in this repository (`TemporalStasis.Example`) that can show you around the API. This is a rewrite of the original Temporal Stasis, and some features (e.g. sending custom packets) aren't implemented yet.

### Requirements

For proxying the zone server, **Temporal Stasis requires the Oodle Network Compression library**. This can be obtained [here](https://github.com/WorkingRobot/OodleUE). This must be present at runtime so Temporal Stasis can load the library (e.g. next to your executable in the build output).

For proxying the lobby server, Temporal Stasis requires some information that may change across game updates:

- Opcode and fields of the `EnterWorld` IPC packet
- Fields and keys of the `EncryptionInit` segment

These are automatically provided by Temporal Stasis for the current game version, but you can override them if that information changes before this library is updated.

### Connecting

To connect to Temporal Stasis, you need to redirect the game client to connect to your proxy instead of the original lobby server, and then your proxy will then connect to the original lobby server. This can be done in a few ways:

- Pass arguments to the game client to override the default lobby server (`DEV.LobbyHost04=127.0.0.1 DEV.LobbyPort04=44994`)
- Set up a custom DNS server and override the lobby server DNS records (`neolobby02.ffxiv.com`)

Those arguments and that DNS record point to the Aether data center. Note that the index from the command line and name of the DNS record do not match up (if you need a reference, see the `WorldDCGroupType` sheet [here](https://v2.xivapi.com/api/sheet/WorldDCGroupType?fields=Name,NeolobbyId)).

### Packet deobfuscation

Since Patch 7.2, some packets from the zone server are obfuscated. Temporal Stasis does not deobfuscate these packets for you, but you can use Temporal Stasis with a library like [Unscrambler](https://github.com/perchbirdd/Unscrambler).
