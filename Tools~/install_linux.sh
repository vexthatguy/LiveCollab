#!/bin/sh
# LiveCollab installer for Linux (POSIX sh)
# Usage: ./install_linux.sh [--prefix DIR] [--project /path/to/UnityProject]
#   --prefix   install location (default: $HOME/.local/share/LiveCollab)
#   --project  also install the Unity package into that project (optional)
# Requires: python3 available on PATH (or a compiled livecollab_host binary
# placed in the payload "livecollab/" folder next to this script).

set -e

VERSION="1.0.1"
SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
PAYLOAD="$SCRIPT_DIR/livecollab"
PREFIX="${HOME}/.local/share/LiveCollab"
PROJECT=""

usage() {
    cat <<EOF
LiveCollab ${VERSION} installer (Linux)

Usage: $0 [options]

Options:
  --prefix DIR    install location (default: ~/.local/share/LiveCollab)
  --project DIR   also install the Unity package into a Unity project
  -h, --help     show this help

After installing, open the Unity project, then use the LiveCollab toolbar
button or Tools > LiveCollab > Open Panel.
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
    echo "ERROR: payload folder not found next to this script: $PAYLOAD" >&2
    echo "Unpack the whole archive (keep livecollab/ next to install_linux.sh)." >&2
    exit 1
fi

# ---- install core ----
echo "Installing LiveCollab ${VERSION} → $PREFIX"
mkdir -p "$PREFIX"
cp -R "$PAYLOAD/." "$PREFIX/"

# keep companion script executable
chmod +x "$PREFIX/livecollab_host.py" 2>/dev/null || true
if [ -f "$PREFIX/livecollab_host" ]; then
    chmod +x "$PREFIX/livecollab_host"
fi

# optional convenience symlink on PATH
if [ -d "${HOME}/.local/bin" ] || mkdir -p "${HOME}/.local/bin" 2>/dev/null; then
    ln -sf "$PREFIX/livecollab_host.py" "${HOME}/.local/bin/livecollab_host.py" 2>/dev/null || true
    echo "  (added ~/.local/bin/livecollab_host.py symlink)"
fi

echo "  companion: $(ls "$PREFIX" | grep -E 'livecollab_host' | tr '\n' ' ')"
echo "  package:   $PREFIX/package"

# ---- optional project install ----
if [ -n "$PROJECT" ]; then
    if [ ! -d "$PROJECT" ] || [ ! -d "$PROJECT/Assets" ]; then
        echo "WARNING: '$PROJECT' does not look like a Unity project (no Assets/)." >&2
    fi
    mkdir -p "$PROJECT/Packages/com.livecollab.editor"
    cp -R "$PAYLOAD/package/." "$PROJECT/Packages/com.livecollab.editor/"
    echo "Package installed into $PROJECT/Packages/com.livecollab.editor"
fi

cat <<EOF

✅ LiveCollab ${VERSION} installed.

Next steps:
  1. Open your Unity project (2020.3+).
  2. Tools > LiveCollab > Open Panel (or the toolbar icon).
  3. Start hosting (uses python3 or a compiled livecollab_host) or join.

Note: the companion only runs while you are hosting — zero footprint otherwise.
EOF
