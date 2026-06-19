# ProxyBase

A lightweight, extensible **man-in-the-middle network proxy** for the MMORPG **Dark Ages** (client **4.21**), written in C# (.NET Framework 4.8, WinForms). It sits between the game client and the game server, decrypts and logs the packet stream in real time, and lets you hook individual packet opcodes to inspect or modify traffic.

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
- Full implementation of the Dark Ages packet encryption (key/salt tables, ordinal-based XOR, MD5 integrity bytes) and the dialog CRC/encryption
- Per-connection UI tab with live incoming/outgoing packet logs
- Simple `opcode → handler` registration model for inspecting or rewriting packets
- In-memory client patching via Win32 P/Invoke (`kernel32`)

## Requirements

- Windows
- .NET Framework 4.8 — built into Windows 10 (1903+) and Windows 11, so end users typically need no extra runtime install. Building requires the .NET Framework 4.8 targeting pack (bundled with current Visual Studio).
- Visual Studio 2013 or newer with C# / .NET Framework desktop support
- A Dark Ages client installed (default path `C:\KRU\Dark Ages\Darkages.exe`)

## Building

1. Open `ProxyBase.sln` in Visual Studio.
2. Make sure the platform is **x86** — the project is x86-only because it patches a 32-bit client.
3. Build (`Ctrl+Shift+B`). Output lands in `ProxyBase\bin\Debug\` (or `bin\Release\`).

## Configuration — this is version- and server-specific

All tweakable settings live in one file: **`ProxyBase/Config.cs`**. This base was built against one specific client build (**4.21**) and one specific server, so to use it against your own setup you'll most likely need to change:

| Setting (`Config.cs`) | Default | Notes |
|---|---|---|
| `RemoteServerIp` / `RemoteServerPort` | `52.88.55.94` / `2610` | The real game server the proxy connects out to. |
| `LocalListenPort` | `2610` | Loopback port the proxy listens on and redirects the client to (kept in sync across the listener, the memory patch, and the redirect rewrite). |
| `ClientPath` | `C:\KRU\Dark Ages\Darkages.exe` | Path to the client executable to launch and patch. |
| `PatchForceJump`, `PatchConnectIp`, `PatchConnectPort`, `PatchSecondJump` | `0x004333A2` … | Client memory offsets that redirect it to localhost. **Specific to one `Darkages.exe` build** — a different client version needs different offsets. |

## Extending it — adding packet handlers

Handlers are registered per opcode in `Server.cs`. Each returns `true` to forward the packet or `false` to drop it. Examples already in the base:

```csharp
// In the Server constructor:
ClientMessageHandlers[0x10] = new ClientMessageHandler(ClientMessage_0x10_ClientJoin);
ServerMessageHandlers[0x03] = new ServerMessageHandler(ServerMessage_0x03_Redirect);

// A handler:
public bool ClientMessage_0x10_ClientJoin(Client client, ClientPacket msg)
{
    var seed = msg.ReadByte();
    var key  = msg.Read(msg.ReadByte());
    var name = msg.ReadString8();
    // ... inspect or modify the packet ...
    return true; // return false to drop it instead of forwarding
}
```

Use the `Packet` reader/writer helpers (`ReadByte`, `ReadString8`, `ReadUInt32`, `WriteString8`, …) to parse and build packet bodies.

## Project structure

| File | Purpose |
|---|---|
| `Program.cs` | Entry point; global sync object and MD5 helper. |
| `Config.cs` | Central settings: server/client endpoints and client memory-patch offsets. |
| `MainForm.cs` | Main window; launches & patches the client; hosts connection tabs. |
| `Server.cs` | Listens for the client, spawns `Client` relays, registers packet handlers. |
| `Client.cs` | One client ↔ server relay: async receive/send loops, queues, key generation. |
| `Packet.cs` | `Packet` base + `ClientPacket` / `ServerPacket`: framing, encryption, dialog crypto, salt & CRC tables. |
| `ClientTab.cs` | Per-connection UI tab with packet logging and manual send/recv. |
| `ProcessMemoryStream.cs` | A `Stream` over another process's memory (used for client patching). |
| `Kernel32.cs` | Win32 P/Invoke declarations. |

## License

[MIT](LICENSE) © 2026 macronator
