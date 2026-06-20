# Dark Ages packet protocol (client 7.41)

A working reference for the wire protocol this proxy speaks, recovered by reverse
engineering the `Darkages.exe` client and cross-checked against several independent
open implementations (pyda, da.js, Chaos, Arbiter). It documents what `Packet.cs`
implements and what the [`ServerOpcode` / `ClientOpcode`](ProxyBase/Opcodes.cs) enums name.

> Names are the cross-verified set. Wire-field layouts are listed only where they are
> high-confidence (hand-verified in the disassembly and agreeing across sources). Many
> more opcodes exist than have byte-exact field layouts — treat an unlisted layout as
> "known opcode, body not field-verified here."

## 1. Framing

```
+------+--------+--------+= = = = = = = = = =+
| 0xAA | len_hi | len_lo |  body (len bytes) |
+------+--------+--------+= = = = = = = = = =+
  [0]    [1]      [2]       [3 .. 3+len-1]
```

- `[0]` = `0xAA` signature (constant).
- `[1..2]` = body length, unsigned **16-bit big-endian**.
- `[3]` = opcode; `[4]` = ordinal/sequence byte **for encrypted opcodes only**.
- All multi-byte fields are **big-endian**.

## 2. Encryption

Each opcode uses one of three schemes. The per-opcode sets below are authoritative
(binary RE, confirmed identical in da.js) and live in `ShouldEncrypt` / `UseDefaultKey`:

| Scheme | C2S opcodes | S2C opcodes |
|--------|-------------|-------------|
| **raw** (no crypto) | `00, 10, 48` | `00, 03, 40` (proxy also treats `7E` as raw — see below) |
| **static key** (`UrkcnItnI`) | `02, 03, 04, 0B, 26, 2D, 3A, 42, 43, 4B, 57, 62, 68, 71, 73, 7B` | `01, 02, 0A, 56, 60, 62, 66, 6F` |
| **dynamic key** (per-session table) | everything else | everything else |

> `0x7E` (S2C) is the pre-key encryption-setup packet. A full client decrypts it with the
> session key; a proxy has no key that early, so this base forwards `0x7E` verbatim (the
> client still decrypts it). That is the one place this base deviates from the raw set.

**Cipher (both keys).** For plaintext byte `i` (key length 9):

```
b[i] ^= salt[(i / 9) % 256] ^ key[i % 9]
if (i / 9) % 256 != ordinal:  b[i] ^= salt[ordinal]
```

`salt` is the 256-byte table for the session `seed` (10 tables, seeds `0x00`–`0x09`).

**Static key** = the constant `"UrkcnItnI"` (`55 72 6B 63 6E 49 74 6E 49`).

**Dynamic key** = 9 bytes pulled from a 1 KB session "key table":

```
key[i] = keyTable[(a + i*(b*b + 9*i)) % 1024]      for i in 0..8
```

`a` (16-bit) and `b` (8-bit) are random per packet and carried, obfuscated, in the trailer.
The **key table** is an MD5 hash-stretch of a login seed string (here the character name):
`t = md5hex(md5hex(name))`, then append `md5hex(t)` 31 times → 1024 chars.

**Trailer** (appended after the ciphertext):

| Direction | bytes |
|-----------|-------|
| C2S | MD5 tag(4) · `(a&0xFF)^0x70` · `b^0x23` · `(a>>8)^0x74` |
| S2C | `(a&0xFF)^0x74` · `b^0x24` · `(a>>8)^0x64` |

**MD5 tag** (C2S only) = `md5(opcode | ordinal | ciphertext)` bytes `[13, 3, 11, 7]`.

**Dialog sub-encryption** — dialog packets (`0x39`/`0x3A`) carry an additional CRC header
and a small byte cipher applied before/after the normal scheme; see
`GenerateDialogHeader` / `EncryptDialog` / `DecryptDialog` in `Packet.cs`.

## 3. Server → client opcodes

| Op | Name | Crypt | Op | Name | Crypt |
|----|------|-------|----|------|-------|
| `00` | ConnectionInfo | raw | `33` | DisplayPlayer | dyn |
| `02` | LoginResult | static | `34` | Legend | dyn |
| `03` | Redirect | raw | `36` | CountryList | dyn |
| `04` | Location | dyn | `37` | AddAppendage | dyn |
| `05` | PlayerId | dyn | `38` | RemoveAppendage | dyn |
| `07` | DisplayNpc | dyn | `39` | Profile | dyn |
| `08` | Statistics | dyn | `3A` | SpellBar | dyn |
| `0A` | SystemMessage | static | `3B` | Heartbeat | dyn |
| `0B` | MoveClient | dyn | `3F` | Cooldown | dyn |
| `0C` | CreatureWalk | dyn | `42` | ExchangeWindow | dyn |
| `0D` | Chat | dyn | `4A` | ProcessBadGuy | dyn |
| `0E` | RemoveCharacter | dyn | `4B` | CancelTrade | dyn |
| `0F` | AddItem | dyn | `4C` | LogOffSignal | dyn |
| `10` | RemoveItem | dyn | `50` | DialogSequence | dyn |
| `11` | CharacterTurn | dyn | `51` | DialogSequence2 | dyn |
| `13` | HpBar | dyn | `56` | MetafileControl | static |
| `15` | MapInfo | dyn | `5B` | ConnectionControl1 | dyn |
| `17` | AddSpell | dyn | `60` | Ok | static |
| `18` | RemoveSpell | dyn | `62` | MetafileControl2 | static |
| `19` | SoundEffect | dyn | `63` | GroupRequestPopup | dyn |
| `1A` | BodyAnimation | dyn | `64` | MapEffect | dyn |
| `1F` | NewMap | dyn | `66` | ConnectionControl3 | static |
| `20` | MapData | dyn | `67` | WorldMapResponse | dyn |
| `21` | MapDataContinued | dyn | `68` | SynchronizeTicks | dyn |
| `29` | SpellAnimation | dyn | `6B` | Metafile | dyn |
| `2C` | AddSkill | dyn | `6D` | MiscState | dyn |
| `2D` | RemoveSkill | dyn | `6F` | (control) | static |
| `2E` | DisplayWorldMap | dyn | | | |
| `2F` | DialogueResponse | dyn | | | |
| `30` | PopupResponse | dyn | | | |
| `31` | MailMenu | dyn | | | |
| `32` | WorldObjectState | dyn | | | |

`0x3B`→`0x45` and `0x68`→`0x75` are the two server heartbeat/ping pairs (client replies).

## 4. Client → server opcodes

| Op | Name | Crypt | Op | Name | Crypt |
|----|------|-------|----|------|-------|
| `00` | VersionConnect | raw | `38` | Refresh | dyn |
| `01` | ClientInfo | dyn | `39` | DialogueSelect | dyn |
| `02` | CreateCharacterA | static | `3A` | PopupSelect | static |
| `03` | LogIn | static | `3B` | BoardInteract | dyn |
| `04` | CreateCharacterB | static | `3E` | UseSkill | dyn |
| `06` | Walk | dyn | `3F` | WorldMapSelect | dyn |
| `08` | Drop | dyn | `43` | ClickCharacter | static |
| `0B` | LogOut | static | `44` | UnequipGear | dyn |
| `0E` | Speak | dyn | `45` | PongA | dyn |
| `0F` | UseSpell | dyn | `47` | RaiseStat | dyn |
| `10` | ClientJoin | raw | `4A` | Exchange | dyn |
| `11` | Turn | dyn | `4D` | SpellLines | dyn |
| `13` | Assail | dyn | `4E` | SkillCaption | dyn |
| `18` | DropGold | dyn | `57` | NpcMenu | static |
| `1B` | Whisper | dyn | `62` | MetafileRequest | static |
| `1C` | UseItem | dyn | `73` | SocialStatus | static |
| `23` | Message | dyn | `75` | PongB | dyn |
| `2D` | RequestProfile | static | `7B` | MetafileRequest2 | static |
| `2E` | Group | dyn | | | |
| `30` | SwapSlots | dyn | | | |

## 5. Verified wire layouts

High-confidence bodies (hand-verified and agreeing across sources), with the matching
`Packet` reader calls. Fields are big-endian.

**S2C**

| Op | Name | Body |
|----|------|------|
| `03` | Redirect | `addr[4], port:u16, seed:u8, keyLen:u8, key[keyLen], name:string8, id:u32` (polymorphic, 2 states; this base rewrites `addr`+`port` to loopback) |
| `05` | PlayerId | `id:u32, u8, u8, u8, u8, u8` |
| `0A` | SystemMessage | `type:u8, length:u16, text[length]` → `ReadByte()`, `ReadUInt16()`, `ReadString(length)` |
| `0B` | MoveClient | `u8, x:u16, y:u16, u16, u16, u8` |
| `0C` | CreatureWalk | `id:u32, x:u16, y:u16, dir:u8` → `ReadUInt32()`, `ReadUInt16()`, `ReadUInt16()`, `ReadByte()` |
| `11` | CharacterTurn | `id:u32, dir:u8` |
| `13` | HpBar | `id:u32, hp:u8, u8, sound:u8` |
| `15` | MapInfo | `mapId:u16, u8, u8, u8, u8, i8` |
| `19` | SoundEffect | `sound:u8, u16` |

**C2S**

| Op | Name | Body |
|----|------|------|
| `06` | Walk | `direction:u8, step:u8` |
| `10` | ClientJoin | `seed:u8, keyLen:u8, key[keyLen], name:string8, id:u32` (this base reads it to build the session key table) |
| `3A` | PopupSelect | `objType:u8, objId:u16, pursuitId:u16 [, step:u8]` |

## Source & credits

Reverse-engineered from the Dark Ages 7.41 client; opcode names and encryption classes
cross-verified against the public pyda, da.js, Chaos and Arbiter projects. This document
describes the protocol of a third-party game for interoperability/educational purposes —
see the repository [README](README.md) disclaimer. Not affiliated with KRU or Nexon.
