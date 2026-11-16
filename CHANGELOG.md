# Changelog

## [1.0.0] - 2026-11-16

Stable release. This is the version to build a game with a friend on.

Highlights since the betas:

- **Component / Inspector property sync** (experimental toggle, on by default):
  script fields, materials, physics, audio, etc. now replicate live via
  `EditorJsonUtility`. Component additions and removals propagate too.
- Everything from the betas: scene transform sync, presence balls + nametags,
  auto-tuned sync speed, standalone companion host (zero footprint until you
  host), LiveCollab Manager app, cross-platform installers, deep Unity
  auto-detection, template injection for new projects, old-version cleanup.

Not yet synced (expected in 1.x): prefab identity (objects are recreated
plain), undo history, play-mode gameplay state. Component sync is
best-effort for references to assets that don't exist on the other machine.

## [0.3.0-beta.3] - 2026-11-16

- Unity detection is broader: scans **all fixed drives** (old code only
  handled default paths), plus per-user installs under
  `%LOCALAPPDATA%\Programs\Unity`, plus legacy registry keys under `Unity
  Technologies`. It still picks the highest installed version.
- The "Detected installs" label on the Unity page **updates live** when you
  browse to or type a folder, and shows every install found.
- New installer option (on by default): **"Enable LiveCollab in new Unity
  projects"** — copies the package into your Unity project templates, so every
  new project already includes LiveCollab. It stays idle (zero CPU/RAM, no
  background processes) until you host or join.
- Uninstaller also removes any package copies it injected into the templates.

## [0.3.0-beta.2] - 2026-11-16

Installer overhaul (fixes from beta.1 feedback):

- **LiveCollab Manager app** (new): a small GUI to start/stop the host, see
  the join code + address, and open the guide/GitHub. Desktop and Start menu
  shortcuts now point to the Manager (not the HTML guide).
- Fixed garbled text in the installer (non-ASCII strings corrupted in ANSI
  builds) — the script is now pure ASCII.
- Much better Unity auto-detection: registry (HKLM + HKCU, both views) plus a
  scan of all fixed drives for Unity Hub installs; picks the **highest
  installed version**, and the page lists everything it found.
- Installer now **silently uninstalls any older LiveCollab version** before
  installing the new one (no more duplicate/stale installs).
- Finish page offers to launch the Manager app.
- Added `livecollab_manager.py` to the package's `Tools~` (source form).

## [0.3.0-beta.1] - 2026-11-16

Cross-platform release:

- **macOS and Linux supported** (the Unity package always was; now installers
  and companion binaries are too).
- Linux installer script (`Tools~/install_linux.sh`) + tarball.
- macOS installer script (`Tools~/install_macos.command`) + zip; native DMG
  built by CI.
- GitHub Actions workflow (`.github/workflows/build-installers.yml`) builds
  Windows NSIS exe, macOS DMG, and Linux tarball on every `v*` tag push and
  attaches them to the release automatically.
- Companion host detection now also finds the Unix-style compiled binary
  (`livecollab_host` without extension) on macOS/Linux.

## [0.2.0-beta.1] - 2026-11-16

First **beta release** — ships as a Windows installer (NSIS):

- `LiveCollab-Setup-0.2.0-beta.1.exe`
  - Installs the full Unity package, the standalone companion host
    (`LiveCollabHost.exe`, prebuilt), the Python scripts, and an offline
    user guide.
  - Auto-detects Unity installations (registry + Unity Hub default folders);
    if none is found you can browse to your Unity folder manually.
  - Optional: installs the package directly into a chosen Unity project's
    `Packages/com.livecollab.editor`.
  - Standard uninstaller, Start Menu shortcuts, desktop shortcut option.

## [0.2.0] - 2026-11-16

- **Standalone host companion** (`Tools~/livecollab_host.py`, pure Python stdlib):
  - Zero resource usage until you press "Start hosting" — nothing runs beforehand.
  - Runs as a background relay: network layer lives outside the Unity editor, so
    hosting survives recompiles/domain reloads and the editor stays lightweight.
  - Single-file EXE build script (`Tools~/build_host_exe.bat`, PyInstaller).
  - Companion assigns collaborator ids/colors and relays presence between guests.
- Host engine choice in the panel: **Companion** (default) or **In-editor**.
- Port-in-use detection before spawning a companion (prevents ghost sessions).

## [0.1.0] - 2026-11-16

Initial release:

- Toolbar button (top-left of the editor toolbar) opens the LiveCollab panel.
- Host mode: TCP server on your PC, LAN auto-discovery, 4-character join code.
- Join mode: join via `ip:port`, or use the LAN discovery list / join code.
- Real-time scene sync: object create/move/rotate/scale/rename/activate/delete.
- Collaborator presence: floating ball + nametag in the Scene view.
- Auto-tuned sync profile from hardware (CPU cores + RAM); manual override available.
- Zero external dependencies; single small package.
