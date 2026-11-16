# Changelog

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
