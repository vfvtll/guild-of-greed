# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Required reading

[CODING_STANDARDS.md](CODING_STANDARDS.md) is the authoritative spec for this repo — naming, layering, file-size caps, Godot/.NET specifics, the Domain/Data layering rule, locale conventions, anti-patterns. Read it before any non-trivial change; it contains rules that are easy to violate by accident (notably: no `using Godot;` anywhere under `shared/`, and the 500-line hard limit per file enforced via `partial class` splits).

## Build, run, test

Monorepo built from `GuildOfGreed.sln` (.NET 8, three projects). There is no test suite yet.

```bash
dotnet build GuildOfGreed.sln                  # builds all three projects
dotnet build server/GuildOfGreed.Server.csproj # server only (no Godot SDK needed)
dotnet build shared/GuildOfGreed.Shared.csproj # portability check — fails if Godot leaked into shared
dotnet run --project server                    # runs the TLS server on 127.0.0.1:5870
```

The client (`client/GuildOfGreed.Client.csproj`) uses `Godot.NET.Sdk/4.6.2`; it is normally built and run from the Godot 4.6 editor, not the CLI. Main scene: [client/scenes/main.tscn](client/scenes/main.tscn). Entry script: [client/src/Core/Main.cs](client/src/Core/Main.cs). `GameData` is the sole autoload (see `[autoload]` in [client/project.godot](client/project.godot)).

Server data (SQLite DB + self-signed TLS cert) lives in `./data/` (configurable via `GOG_DATA_DIR`). `GOG_HOST` and `GOG_PORT` override defaults (`127.0.0.1:5870`). When the server starts it prints the cert SHA-1 thumbprint — needed if a new client refuses to TOFU-pin automatically.

Docker: `docker compose up -d --build` runs the server in a container; persistent data is bind-mounted at `./docker-data`.

## Architecture

### Three-project layering

```
client/  (Godot 4.6 + C#)  ─┐
                            ├──► shared/  (POCO + game rules, no Godot)
server/  (C# .NET 8)        ─┘
```

Both client and server reference `shared` via `ProjectReference`. The layering is strictly one-way: `shared/Domain` knows nothing about `shared/Data`; `shared/*` knows nothing about Godot, client, or server. Violating this breaks the server build immediately because the server has no Godot SDK. See §1 of CODING_STANDARDS.md.

### Shared = single source of truth for game rules

[shared/src/Domain/](shared/src/Domain/) — POCO entities (`CharacterData`, `EnemyData`, `RunMap`, `StatusEffect`, `Inventory`, `RandomSource`/`Rng`). Serializable to JSON for both save files and the wire protocol.

[shared/src/Data/](shared/src/Data/) — static DBs and pure computation: `CardsDB` (with `Compute*` damage/block/heal formulas), `ItemsDB`, `PotionsDB`, `MapGenerator`. **All gameplay formulas live here**; the client View calls the same `Compute*` methods used by [shared/src/Combat/CombatEngine.cs](shared/src/Combat/CombatEngine.cs) — there is intentionally no "display vs. actual" split.

[shared/src/Combat/CombatEngine.cs](shared/src/Combat/CombatEngine.cs) is a deterministic pure-function engine: `(BattleState, BattleAction) → List<BattleEvent>`. The whole point is that client and server, given the same `Seed`, produce identical events — that's how anti-cheat/desync detection works without trusting the client.

All randomness in `shared` must go through `Rng`/`RandomSource` (NOT `GD.Randi()` or `new Random()`), so battles are replayable from `(initial state, seed, action sequence)`.

### Network protocol

[shared/src/Net/](shared/src/Net/) defines the wire format used by both ends:

- [Messages.cs](shared/src/Net/Messages.cs) — `ClientMessage` / `ServerMessage` polymorphic trees, serialized with MessagePack via `[Union]` discriminators. Each field is `[Key(N)]`-tagged; **N is never reused after removal** (breaks old clients).
- [MessageFraming.cs](shared/src/Net/MessageFraming.cs) — 4-byte big-endian length prefix + payload, 1 MiB cap per frame.
- [ProtocolVersion.cs](shared/src/Net/ProtocolVersion.cs) — `Current` integer. **Any change to `Messages.cs` (new type, new field, rename) must bump `Current`.** The server decides compatibility in [Session.cs](server/src/Session.cs) (`MinSupportedClientVersion`); incompatible clients see the `UpdateRequiredView`.

Transport is TCP + TLS 1.2/1.3 with a self-signed cert. The client TOFU-pins the server cert via [client/src/Net/ServerTrustStore.cs](client/src/Net/ServerTrustStore.cs).

### Server architecture

[server/Program.cs](server/Program.cs) opens SQLite + cert, then runs [Listener.cs](server/src/Listener.cs) which fires up one [Session.cs](server/src/Session.cs) per TCP connection. Session is a small state machine: Handshake → Unauthenticated → Authenticated → (Playing). Persistence is SQLite via [AccountStore.cs](server/src/AccountStore.cs); schema migrations are in [server/src/Db/](server/src/Db/). `CharacterData` is stored as a JSON blob (schema churns; only stable fields get their own columns).

### Client architecture

[client/src/Core/Main.cs](client/src/Core/Main.cs) is the root router: Connecting → handshake → Auth or Resume → CharacterSelect → CharacterCreation/LocationSelect → MapView → Combat → loop. `NetworkClient` events fire on a network task — Main marshals back to the Godot main thread via `CallDeferred`.

`GameData` (autoload) holds the single static `Instance`, current `UserSession`, locale, network client reference. `UserSession.Can(Permission.X)` is the permission gate; both client (UX) and server (authoritative) consult `PermissionsDB` from `shared`.

Feature folders under [client/src/](client/src/) follow View/Controller split: View classes (`CardView`, `EnemyView`, etc.) only render and emit signals; the feature Controller (e.g. `Combat`) coordinates between Views and `shared` Domain/Data. The Combat controller is split into `Combat.cs` / `Combat.Cards.cs` / `Combat.UI.cs` / `Combat.Animations.cs` via `partial class` to stay under the 500-line cap.

UI styling goes through [client/src/Combat/UIStyle.cs](client/src/Combat/UIStyle.cs); never hardcode colors in feature code. Player-facing strings should go through `Lang.T(key)` ([client/src/Core/Lang.cs](client/src/Core/Lang.cs)) with `snake_case.dotted` keys, default locale `Ru`.

## Adding things — quick map

- **New card** → entry in `CardsDB.Cards` (and `case` in `Combat.PlayCard` switch if new behavior).
- **New enemy** → factory method `EnemyData.CreateXxx()`.
- **New location** → entry in `GameData.LocationNames` + case in `SpawnEnemies()`.
- **New gameplay formula** → put it in `shared/Data`, not `client/`. Both sides must compute identically.
- **New wire message** → add to `Messages.cs` union list, bump `ProtocolVersion.Current`, handle in `Session` state machine.
