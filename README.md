# ProxyBase

A lightweight, extensible **man-in-the-middle network proxy** for the MMORPG **Dark Ages** (client **7.41**), written in C# (.NET Framework 4.8, WinForms). It sits between the game client and the game server, decrypts and logs the packet stream in real time, and lets you hook individual packet opcodes to inspect or modify traffic.

It is meant as a **starting point (a "base")** for building your own tools — packet analyzers, automation, research utilities, and the like.

> ## ⚠️ Disclaimer — read this first
>
> This project is provided for **educational and research purposes only**.
>
> Automating, botting, or otherwise modifying your interaction with Dark Ages **violates the game's Terms of Service**. Using this software may result in account suspension or other action by the game's operators. **You use it entirely at your own risk and assume full responsibility for how you use it.** The software is provided "AS IS", without warranty of any kind — see [LICENSE](LICENSE).
>
> This project is **not affiliated with, endorsed by, or associated with KRU Interactive or Nexon**. "Dark Ages" and all related names are trademarks of their respective owners.

## How it works

1. A `TcpListener` listens on `127.0.0.1:2610`.
2. From **File → Launch Dark Ages**, the proxy starts `Darkages.exe` suspended and patches its memory (via `ReadProcessMemory` / `WriteProcessMemory`) so the client connects to `127.0.0.1:2610` instead of the live server, then resumes the process.
3. When the client connects, the proxy opens its own connection to the **real** server (`52.88.55.94:2610` by default) and relays traffic in both directions.
4. Each packet is parsed (`ClientPacket` / `ServerPacket`), decrypted, logged to a per-connection tab, optionally passed through a registered handler, re-encrypted, and forwarded.
5. Server **redirect** packets (e.g. login server → world server) are intercepted so the proxy follows the client to the next server while keeping it pointed at localhost.

## Features

- Transparent client ↔ server packet relay over loopback
- Full implementation of the Dark Ages packet encryption (key/salt tables, ordinal-based XOR, MD5 integrity bytes) and the dialog CRC/encryption — cross-checked against the reverse-engineered client
- Named `ClientOpcode` / `ServerOpcode` enums (`Opcodes.cs`) recovered from the protocol, so handlers read by name instead of magic numbers
- Per-connection UI tab with live incoming/outgoing packet logs
- Simple `opcode → handler` registration model for inspecting or rewriting packets
- In-memory client patching via Win32 P/Invoke (`kernel32`)

## Requirements

- Windows
- .NET Framework 4.8 — built into Windows 10 (1903+) and Windows 11, so end users typically need no extra runtime install. Building requires the .NET Framework 4.8 targeting pack (bundled with current Visual Studio).
- Visual Studio 2019 or newer (the project uses the modern SDK-style format), or the .NET SDK / MSBuild for command-line builds
- A Dark Ages client installed (default path `C:\KRU\Dark Ages\Darkages.exe`)

## Building

Open `ProxyBase.sln` in Visual Studio (2019+) and build (`Ctrl+Shift+B`), or from a terminal in the repo root:

```
dotnet build
```

The project targets .NET Framework 4.8 and is pinned to **x86** (it patches a 32-bit client), so there's no platform to select. Output lands in `ProxyBase\bin\Debug\net48\` (or `bin\Release\net48\`).

## Usage

1. Build and run `ProxyBase.exe`.
2. **Options → Choose DA Path…** — type your `Darkages.exe` path or click **Browse** to locate it, then **OK**. Your choice is remembered across runs. (If it's unset, **Launch Dark Ages** opens this dialog the first time, pre-filled with the `Config.cs` default.)
3. **File → Launch Dark Ages** — the proxy starts the client, patches it to connect through the proxy, and a tab opens showing the live packet stream.

> **Note:** only one program can listen on the proxy port (`2610`) at a time. If another ProxyBase instance or a bot (e.g. Ascend) is already running, close it first — otherwise ProxyBase shows a "port in use" message and exits.

## Configuration — this is version- and server-specific

All tweakable settings live in one file: **`ProxyBase/Config.cs`**. This base was built against one specific client build (**7.41**) and one specific server, so to use it against your own setup you'll most likely need to change:

| Setting (`Config.cs`) | Default | Notes |
|---|---|---|
| `RemoteServerIp` / `RemoteServerPort` | `52.88.55.94` / `2610` | The real game server the proxy connects out to. |
| `LocalListenPort` | `2610` | Loopback port the proxy listens on and redirects the client to (kept in sync across the listener, the memory patch, and the redirect rewrite). |
| `ClientPath` | `C:\KRU\Dark Ages\Darkages.exe` | Default client path. You can also set it at runtime via **Options → Choose DA Path…** (remembered across runs), which overrides this. |
| `PatchForceJump`, `PatchConnectIp`, `PatchConnectPort`, `PatchSecondJump` | `0x004333A2` … | Client memory offsets that redirect it to localhost. **Specific to one `Darkages.exe` build** — a different client version needs different offsets. |

## Extending it — adding packet handlers

Handlers are registered per opcode in `Server.cs`. Each returns `true` to forward the packet or `false` to drop it. Examples already in the base:

```csharp
// In the Server constructor (opcodes are named in Opcodes.cs):
ClientMessageHandlers[(byte)ClientOpcode.ClientJoin] = new ClientMessageHandler(ClientMessage_0x10_ClientJoin);
ServerMessageHandlers[(byte)ServerOpcode.Redirect]      = new ServerMessageHandler(ServerMessage_0x03_Redirect);

// A handler:
public bool ServerMessage_0x0A_SystemMessage(Client client, ServerPacket msg)
{
    var type   = msg.ReadByte();         // wire layout from the protocol RE:
    var length = msg.ReadUInt16();       //   type:u8, length:u16(BE), text[length]
    var text   = msg.ReadString(length);
    // ... inspect or modify the packet ...
    return true; // return false to drop it instead of forwarding
}
```

Use the `Packet` reader/writer helpers (`ReadByte`, `ReadString8`, `ReadUInt32`, `WriteString8`, …) to parse and build packet bodies. `Server.cs` ships two read-only example handlers (`ServerMessage_0x0A_SystemMessage`, `ServerMessage_0x0C_CreatureWalk`) that decode a packet per its reverse-engineered layout and log the fields — delete them or use them as templates.

See **[PROTOCOL.md](PROTOCOL.md)** for the full opcode tables (names + encryption class), the encryption scheme, and the verified wire layouts.

## Project structure

| File | Purpose |
|---|---|
| `Program.cs` | Entry point; global sync object and MD5 helper. |
| `Config.cs` | Central settings: server/client endpoints and client memory-patch offsets. |
| `MainForm.cs` | Main window; launches & patches the client; hosts connection tabs. |
| `Server.cs` | Listens for the client, spawns `Client` relays, registers packet handlers. |
| `Client.cs` | One client ↔ server relay: async receive/send loops, queues, key generation. |
| `Packet.cs` | `Packet` base + `ClientPacket` / `ServerPacket`: framing, encryption, dialog crypto, salt & CRC tables. |
| `Opcodes.cs` | `ClientOpcode` / `ServerOpcode` enums: named packet opcodes recovered from the protocol. |
| `ClientTab.cs` | Per-connection UI tab with packet logging and manual send/recv. |
| `ProcessMemoryStream.cs` | A `Stream` over another process's memory (used for client patching). |
| `Kernel32.cs` | Win32 P/Invoke declarations. |

## Provenance & credits

The original **ProxyBase** for Dark Ages was created by **Acht** (~2012). This repository is that community base — cleaned up and **ported to .NET Framework 4.8** by **macronator** (2026) and republished so others can build on it.

macronator claims **no ownership** of the original work and shares it in good faith for educational use. If you are the original author (or hold rights to the original code) and would like different credit or removal, please open an issue.

## License

This project is released under the [MIT License](LICENSE).

Original base © 2012 **Acht** · cleaned up, ported to .NET Framework 4.8, and republished by **macronator** (2026).
