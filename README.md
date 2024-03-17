# Temporal Stasis

FFXIV network proxy library.

## Features

- On the fly packet modification - modify and drop, with support for decryption and re-encryption
- (Mostly untested) Arbitrary packet sending
- Custom logic controllable through C# events

## Support

- [x] Lobby
  - [x] Decrypting Blowfish
  - [x] Rewriting server transfers
- [x] Zone/Chat
  - [x] Decompressing Oodle
  - [ ] Rewriting server transfers

## TODO

- [ ] Make it performant jfc
- [ ] Test packet sending
- [ ] Support Zlib
