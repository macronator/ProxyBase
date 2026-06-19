# async-relay — work-in-progress notes

This branch rewrites the packet relay from the original **thread-per-client +
`Thread.Sleep(1)` busy-loop** (with `BeginReceive`/`BeginSend` APM) to **`async`/`await`**:
two async receive pumps + two async send pumps per client, with the send queues kept so
manual injection (`Client.Enqueue`, used by the ClientTab Send/Recv buttons) still works
and each socket keeps a single writer.

**It is NOT merged to `main`.** `main` uses the original threaded relay and works.

## Status

- ✅ Relays the **entire encrypted login handshake** correctly, both directions — verified
  live: `0x7E` (encryption init), `0x62`, `0x00`, `0x57` (version), then the `0x03` redirect.
  Crypto, per-direction ordinals, framing, and dialog handling all work.
- ❌ **The client does not reconnect after the server's `0x03` redirect**, so it gets stuck
  at the title screen. On `main`, the same client + server reconnect and reach the game.

(Tested against a Dark Ages **7.41** client.)

## Verified / ruled out (via temporary `Debug.WriteLine` instrumentation)

- The rewritten redirect is **byte-perfect**: body begins `01-00-00-7F-0A-32`
  (= `127.0.0.1:2610`) followed by a valid seed/key/name — identical to what `main` produces.
- The redirect is **fully flushed** to the client before teardown (a `done` log fired after
  the `WriteAsync` and before the client closed). So it is **not** being truncated.
- The accept loop is **alive and listening** for the reconnect.
- It is **not** an `Invoke`/lock deadlock — every `handler -> log -> queued` step completes.
- **Tried and did NOT help:** delaying/gracefully flushing the teardown (`Thread.Sleep` +
  release-then-close in `Disconnect`) on the theory the redirect send was being aborted. The
  send was already completing, so this was a dead end — revert it if still present.

**Conclusion:** every *application-level* behavior is correct and identical to `main`, yet the
client behaves differently. The difference is **below the app layer** — how the async socket
teardown looks on the wire.

## Most likely cause & next step

When the client half-closes after the redirect (receive returns 0 bytes), the async path
tears the connection down differently than `main`'s loop did. Leading suspect: **FIN vs RST**
(or close timing). A client often treats a clean `FIN` as "follow the redirect, reconnect"
but an `RST` as "connection failed, give up."

**Next step: capture with Wireshark** (Windows loopback via Npcap's loopback adapter), run
`main` and this branch back to back, and compare the teardown of the *first* connection right
after the `0x03` redirect:

1. Does the client send a `SYN` to `127.0.0.1:2610` after the redirect?
   - No SYN  → the client chose not to reconnect (reacting to how the connection died).
   - SYN but no proxy `accept` → accept-side problem.
2. Is the first connection closed with `FIN` (graceful) or `RST` (abort)?
3. Diff the two captures to find the packet that differs.

If `async` sends an `RST` where `main` sends a `FIN`, fix the teardown to close gracefully —
e.g. `Socket.Shutdown(SocketShutdown.Both)` and drain before `Close()`, or restructure so a
one-sided receive-0 doesn't immediately abort the whole connection (shut down only that
direction; tear the rest down once both sides are done).

## Re-adding the diagnostic logging

The traces came from `Debug.WriteLine` calls (shown in VS **Output -> Debug**) at:

- the ctor, after the upstream `Connect` (`connected to upstream <ep>`);
- each receive: `read N bytes` / `peer closed (count == 0)` / `bad first byte`;
- each `ProcessReceived`: opcode + `handler -> log -> queued`;
- each send: `writing N bytes` / `done`;
- `ServerMessage_0x03_Redirect`: `BitConverter.ToString(msg.BodyData)` + the new upstream;
- `AcceptLoopAsync`: `waiting for a client …` / `client connected`;
- `Disconnect`: `tearing down`.
