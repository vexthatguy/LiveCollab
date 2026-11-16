# LiveCollab

Real-time multiplayer editing **inside the Unity Editor**.

Host a project from your PC, let friends join with a 4-character join code
(LAN) or an `IP:port` (internet), and edit the same scene live together —
create objects, move them, rotate, rename, delete — all visible in real time.
Every collaborator appears in your Scene view as a small floating ball with a
nametag, like a co-editing session in Roblox Studio.

- Zero external dependencies (pure .NET + Unity editor APIs)
- Lightweight: one small editor package, no background services
- Auto-tuned sync rate from your hardware (CPU cores + RAM), manual override included
- Works on any computer with Unity 2020.3+ (Windows, macOS, Linux)
- **Zero-footprint standalone host companion**: a single-file Python script (or a
  compiled EXE) that ships with the package and **does not run at all until you
  press "Start hosting"** — no CPU/RAM use in idle projects
- Installers: Windows setup exe (NSIS), macOS app bundle/DMG, Linux installer
  script/tarball

## Contents

- [How it works](#how-it-works)
- [Requirements](#requirements)
- [Installation](#installation)
- [Quick start](#quick-start)
- [Joining over the internet](#joining-over-the-internet)
- [Sync speed (auto-tuned)](#sync-speed-auto-tuned)
- [What syncs](#what-syncs)
- [Security / trust model](#security--trust-model)
- [Troubleshooting](#troubleshooting)
- [Roadmap](#roadmap)
- [License](#license)

## How it works

```
 Host machine                            Guest machine
┌──────────────────────────┐            ┌──────────────────────────┐
│ LiveCollab host session  │  TCP  9501 │ LiveCollab client session │
│ (authoritative scene)    │◄──────────►│ (mirrors host scene)      │
│                          │            │                          │
│ UDP broadcast 9595 ◄─────┼────────────│──── discovery beacons      │
└──────────────────────────┘            └──────────────────────────┘
```

The host owns the authoritative scene state. Clients send their local edits
(create/move/delete) to the host; the host applies them, then broadcasts the
delta to everyone. A background diff engine compares the scene hierarchy
against the last known state, so only what changed travels over the wire.

### Standalone host companion (zero footprint)

The package ships a companion `:Python script / EXE) that acts as the network
relay for your session:

- **It does not run until you start hosting.** No file watchers, no processes,
  no CPU/RAM usage in a project you're just opening.
- When you press **Start hosting** with the *Companion* engine (default), Unity
  launches it, it takes over the TCP listener + LAN discovery, and the Unity
  editor connects to it locally as the session owner.
- Guests connect the same way as before (join code / IP:port / discovery list).
- Because the network layer is outside the editor, **hosting survives
  recompiles and domain reloads** — the companion keeps the room alive and the
  editor reconnects.

**Where does it live?** Inside the package at `Tools~/livecollab_host.py`
(also auto-detected from `<YourProject>/LiveCollab_Host/livecollab_host.py`
if you copy it there).

**Build an EXE (optional, recommended for non-Python machines):**

```
pip install pyinstaller
Tools~/build_host_exe.bat
```

Then copy `dist/LiveCollabHost.exe` into `<YourProject>/LiveCollab_Host/`.
Unity will prefer the EXE over Python automatically. If neither Python nor an
EXE is found, the panel warns you and you can fall back to the **In-editor**
engine.

### Installing on macOS and Linux

The Unity package itself is fully cross-platform — the differences are only in
how you install it and which companion binary you use.

**macOS (two options):**

- **CI-built DMG** — native `.dmg` installers are built automatically by GitHub
  Actions for every version tag (see *Releases*). Mount the DMG and run
  `install.command`.
- **Manual** — download the macOS zip, unzip, and run `install.command` from a
  terminal (or right-click → Open if Gatekeeper complains). It installs to
  `~/Library/Application Support/LiveCollab`.

**Linux (two options):**

- **CI-built tarball** — download `LiveCollab-Linux-<version>.tar.gz`, extract,
  and run `./install.sh` (POSIX sh). Installs to `~/.local/share/LiveCollab`.
- **Manual** — same archive contains the raw payload (`livecollab/`) you can
  place anywhere; `install.sh` supports `--prefix` and `--project`.

Both installers accept `--project /path/to/UnityProject` to install the package
directly into that project's `Packages/com.livecollab.editor`.

> Compiled `livecollab_host` companion binaries for macOS/Linux are produced by
> GitHub Actions on tag pushes; until then the bundled `livecollab_host.py`
> works with `python3`.

## Requirements

- Unity **2020.3** or newer (Editor, any platform: Windows / macOS / Linux)
- Same local network for auto-discovery (or port forwarding for internet joins)
- The companion host needs Python 3 on PATH, or the prebuilt companion binary:
  - Windows: `LiveCollabHost.exe` (ships inside the setup exe)
  - macOS / Linux: `livecollab_host` binary (built by CI — see below), or use
    the bundled `livecollab_host.py` with `python3`

## Installation

The package is a standard UPM (Unity Package Manager) package. Either way:

**Option A — copy into your project (recommended):**

1. Copy the whole `com.livecollab.editor` folder (this repo) into
   `Assets/../Packages/com.livecollab.editor` inside your Unity project
   (i.e. next to your project's `Packages/manifest.json`).
2. Re-open the project. Unity detects the local package automatically.

**Option B — add from disk:**

1. Copy the folder anywhere on your machine.
2. In Unity: **Window ▸ Package Manager ▸ + ▸ Add package from disk…**
3. Select the `package.json` inside this folder.

After either option you'll see a new **LiveCollab** button at the top-left of
the editor toolbar.

## Quick start

1. **Everyone installs the package** (same version).
2. **Host**: on the hosting PC, open the LiveCollab panel (toolbar button or
   *Tools ▸ LiveCollab ▸ Open Panel*), enter a name, and press **Start hosting**
   (Companion engine is the default — see the standalone companion section
   above). Note the join code and the address shown.
3. **Join (LAN)**: on the guest PC, open the panel, and either
   - type the host's **join code** (it will match the host discovered on your
     network), or
   - click the host in the **Found on this network** list.
4. You should connect, see a snapshot of the host's scene, and see each other
   as floating balls in the Scene view. Move around the Scene view — your ball
   follows your camera position.

> **Tip:** to test on one machine, open two separate Unity Editor instances of
> the same project, host on port `9501` in one, and join `127.0.0.1:9501`
> in the other.

## Joining over the internet

- The host must forward **TCP 9501** (or the port they chose) to their PC.
- Guests join using `PublicIP:port` (e.g. `83.xx.xx.xx:9501`).
- The 4-character join code is a convenience for LAN discovery; over the
  internet you need the IP address.

## Sync speed (auto-tuned)

LiveCollab reads your machine's CPU core count and total RAM and picks a sync
profile automatically:

| Profile | Rate | Typical hardware |
|---------|------|------------------|
| High | ~20 updates/s | 8+ cores / strong machine |
| Medium | ~10 updates/s | 4–8 cores |
| Low | ~5 updates/s | weak/low-power machines |

Override it any time under **Advanced settings ▸ Sync speed profile**.

## What syncs

- GameObject **create / delete**
- **Transform**: position, rotation, scale (local)
- **Parent** hierarchy changes
- **Name** and **active** state

Not synced (v0.1): component property values, prefab identity, undo history,
physics state, play-mode gameplay data.

## Security / trust model

LiveCollab uses plain TCP with no encryption. Treat it like a trusted-party
remote desktop: only invite people you trust, and don't expose the host port
to the public internet unless you understand the risk. A collaborator with
scene access can create/delete/modify objects in your project.

## Troubleshooting

**Guests can't see the host in the discovery list**

- Both machines must be on the same LAN/subnet and allow UDP **9595**.
- Windows Firewall: allow Unity through on first run (or add rules for
  TCP 9501 / UDP 9595).

**Dialog never connects**

- Check the IP/port. Ping the host from the guest.
- On Windows, confirm inbound TCP 9501 is allowed.

**"Port already in use" when starting to host**

- Another LiveCollab host or relay is already running on that port. Stop it
  (or close the other project's hosting session) or choose a different port.

**"Python not found / no companion" warning**

- The companion engine needs either Python 3 on PATH or a built
  `LiveCollabHost.exe` in `LiveCollab_Host/` (see the standalone companion
  section). Or switch the Host engine to **In-editor**.

**Objects flicker / jump**

- Lower the sync profile in Advanced settings, or check CPU load.

**Toolbar button missing**

- Use *Tools ▸ LiveCollab ▸ Open Panel* — the toolbar hook can fail on
  non-standard editor layouts.

## Roadmap

- [ ] Component property sync (Inspector values)
- [ ] Join-code routing relay for easy internet joins (no port forwarding)
- [ ] Prefab-aware creation
- [ ] Ping/quality-based adaptive auto-tuning

## License

MIT — see [LICENSE](LICENSE).
