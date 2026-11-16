#!/bin/bash
# LiveCollab installer for macOS
# Usage: ./install_macos.command [--prefix DIR] [--project /path/to/UnityProject]
# Double-click runs it in Terminal. If Gatekeeper complains, right-click the
# file and choose Open.
# Requires: python3 on PATH (macOS ships Python 3 via Xcode CLT), or a
# compiled `livecollab_host` binary in the payload folder.

set -e
cd "$(cd "$(dirname "$0")" && pwd)"

VERSION="0.3.0-beta.3"
PAYLOAD="$(pwd)/livecollab"
PREFIX="$HOME/Library/Application Support/LiveCollab"
PROJECT=""

usage() {
    cat <<EOF
LiveCollab ${VERSION} installer (macOS)

Usage: $0 [options]

Options:
  --prefix DIR    install location (default: ~/Library/Application Support/LiveCollab)
  --project DIR   also install the Unity package into a Unity project
  -h, --help     show this help
EOF
}

while [ $# -gt 0 ]; do
    case "$1" in
        --prefix) PREFIX="$2"; shift 2 ;;
        --project) PROJECT="$2"; shift 2 ;;
        -h|--help) usage; exit 0 ;;
        *) echo "Unknown option: $1" >&2; usage >&2; exit 1 ;;
    esac
done

if [ ! -d "$PAYLOAD" ]; then
    echo "ERROR: payload folder not found: $PAYLOAD" >&2
    echo "Unpack the whole archive (keep livecollab/ next to install_macos.command)." >&2
    exit 1
fi

echo "Installing LiveCollab ${VERSION} → $PREFIX"
mkdir -p "$PREFIX"
ditto "$PAYLOAD" "$PREFIX"
chmod +x "$PREFIX/livecollab_host.py" 2>/dev/null || true
if [ -f "$PREFIX/livecollab_host" ]; then
    chmod +x "$PREFIX/livecollab_host"
fi

if [ -n "$PROJECT" ]; then
    if [ ! -d "$PROJECT" ] || [ ! -d "$PROJECT/Assets" ]; then
        echo "WARNING: '$PROJECT' does not look like a Unity project (no Assets/)." >&2
    fi
    mkdir -p "$PROJECT/Packages/com.livecollab.editor"
    ditto "$PAYLOAD/package" "$PROJECT/Packages/com.livecollab.editor"
    echo "Package installed into $PROJECT/Packages/com.livecollab.editor"
fi

cat <<EOF

✅ LiveCollab ${VERSION} installed.

Next steps:
  1. Open your Unity project (2020.3+).
  2. Tools > LiveCollab > Open Panel (or the toolbar icon).
  3. Start hosting or join.

Note: the companion only runs while you are hosting — zero footprint otherwise.
EOF
