#!/usr/bin/env python3
"""LiveCollab Manager - small tray-like control app for the companion host.

Ships next to livecollab_host (exe or .py). Provides:
  - start/stop the host relay
  - shows join code, address, status
  - quick links to the guide and GitHub
Pure stdlib (tkinter + subprocess). Works on Windows/macOS/Linux.
"""

import os
import random
import socket
import string
import subprocess
import sys
import tkinter as tk
from tkinter import messagebox, scrolledtext

DEFAULT_PORT = 9501

ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"


def here_dir():
    a0 = sys.argv[0]
    if a0 and os.path.basename(a0) not in ("-c", "") and os.path.exists(a0):
        return os.path.dirname(os.path.abspath(a0))
    return os.path.dirname(os.path.abspath(__file__))


def _registry_install_locations():
    """Best-effort: find the LiveCollab install dir from the registry."""
    paths = []
    if os.name != "nt":
        return paths
    import winreg
    key_path = r"Software\Microsoft\Windows\CurrentVersion\Uninstall\LiveCollab"
    for hive in (winreg.HKEY_LOCAL_MACHINE, winreg.HKEY_CURRENT_USER):
        try:
            with winreg.OpenKey(hive, key_path) as k:
                val, _ = winreg.QueryValueEx(k, "InstallLocation")
                if val:
                    paths.append(val)
        except OSError:
            continue
    return paths


def candidate_dirs():
    dirs = [here_dir(), os.path.dirname(here_dir())]
    dirs += _registry_install_locations()
    seen, out = set(), []
    for d in dirs:
        if not d:
            continue
        d = os.path.abspath(d)
        if d in seen:
            continue
        seen.add(d)
        out.append(d)
    return out


def host_binary():
    """Prefer a saved manual location, then the compiled binary, then the
    python script. Searches are case-insensitive (filesystem-safe)."""
    # 1) remember a manually located host
    saved = _load_saved_host()
    if saved and os.path.exists(saved):
        return saved

    wanted = {n.lower(): n for n in ("livecollab_host.exe", "livecollab_host", "livecollab_host.py")}
    for d in candidate_dirs():
        try:
            entries = os.listdir(d)
        except OSError:
            continue
        for entry in entries:
            hit = wanted.get(entry.lower())
            if hit is None:
                continue
            full = os.path.join(d, entry)
            if hit.endswith(".py"):
                if getattr(sys, "frozen", False):
                    continue  # frozen exe cannot host python child scripts
                return [sys.executable, full]
            return full
    return None


CONFIG_NAME = "manager_config.json"


def _config_path():
    return os.path.join(here_dir(), CONFIG_NAME)


def _load_saved_host():
    try:
        import json
        with open(_config_path(), "r", encoding="utf-8") as f:
            return json.load(f).get("host_path", "")
    except Exception:
        return ""


def _save_host(path):
    try:
        import json
        with open(_config_path(), "w", encoding="utf-8") as f:
            json.dump({"host_path": path}, f)
    except Exception:
        pass


def local_ip():
    try:
        s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        s.connect(("8.8.8.8", 80))
        ip = s.getsockname()[0]
        s.close()
        return ip
    except Exception:
        return "127.0.0.1"


def make_code():
    return "".join(random.choice(ALPHABET) for _ in range(4))


class Manager:
    def __init__(self, root):
        self.root = root
        root.title("LiveCollab Manager")
        root.geometry("460x420")
        root.resizable(False, False)
        try:
            icon = os.path.join(here_dir(), "livecollab_icon.ico")
            if os.path.exists(icon):
                root.iconbitmap(icon)
        except Exception:
            pass

        self.proc = None
        self.code = make_code()
        self.port = tk.StringVar(value=str(DEFAULT_PORT))
        self.name = tk.StringVar(value=os.environ.get("USERNAME") or os.environ.get("USER") or "Creator")
        self.status = tk.StringVar(value="Idle")

        self._build_ui()

    def _build_ui(self):
        pad = {"padx": 10, "pady": 4}

        tk.Label(self.root, text="LiveCollab Manager", font=("Segoe UI", 13, "bold")).pack(anchor="w", **pad)

        frm = tk.Frame(self.root)
        frm.pack(fill="x", **pad)
        tk.Label(frm, text="Your name:").grid(row=0, column=0, sticky="w")
        tk.Entry(frm, textvariable=self.name, width=28).grid(row=0, column=1, sticky="w")
        tk.Label(frm, text="Port:").grid(row=1, column=0, sticky="w", pady=(6, 0))
        tk.Entry(frm, textvariable=self.port, width=10).grid(row=1, column=1, sticky="w", pady=(6, 0))

        btns = tk.Frame(self.root)
        btns.pack(fill="x", **pad)
        self.start_btn = tk.Button(btns, text="Start Host", width=12, command=self.start_host)
        self.start_btn.pack(side="left")
        self.stop_btn = tk.Button(btns, text="Stop Host", width=12, command=self.stop_host, state="disabled")
        self.stop_btn.pack(side="left", padx=(8, 0))

        self.status_lbl = tk.Label(self.root, textvariable=self.status, fg="#0e7c9a",
                                   font=("Segoe UI", 10, "bold"))
        self.status_lbl.pack(anchor="w", **pad)

        self.info = tk.Label(self.root, text="", justify="left", anchor="w", fg="#222")
        self.info.pack(anchor="w", **pad)

        self.log = scrolledtext.ScrolledText(self.root, height=8, state="disabled", font=("Consolas", 9))
        self.log.pack(fill="both", expand=True, padx=10, pady=(0, 6))

        links = tk.Frame(self.root)
        links.pack(fill="x", padx=10, pady=(0, 8))
        tk.Button(links, text="Open User Guide", command=self.open_guide).pack(side="left")
        tk.Button(links, text="GitHub", command=self.open_github).pack(side="left", padx=(8, 0))
        tk.Button(links, text="Locate host...", command=self.locate_host).pack(side="left", padx=(8, 0))
        tk.Button(links, text="Close", command=self.root.destroy).pack(side="right")

        self.root.protocol("WM_DELETE_WINDOW", self.on_close)
        self._log("Ready. Press Start Host to begin.\n")

    def locate_host(self):
        from tkinter import filedialog
        path = filedialog.askopenfilename(
            title="Locate the LiveCollab companion host",
            filetypes=[("Companion host", "*.exe"), ("Python script", "*.py"), ("All files", "*.*")])
        if not path:
            return
        name = os.path.basename(path).lower()
        if name not in ("livecollab_host.exe", "livecollab_host", "livecollab_host.py"):
            messagebox.showwarning("LiveCollab", "That doesn't look like a LiveCollab host file.")
            return
        _save_host(path)
        self._log("Host located: %s\n" % path)

    # ---------- host control ----------
    def start_host(self):
        if self.proc is not None and self.proc.poll() is None:
            messagebox.showinfo("LiveCollab", "Host is already running.")
            return
        try:
            port = int(self.port.get())
            if not (1024 <= port <= 65535):
                raise ValueError
        except ValueError:
            messagebox.showerror("LiveCollab", "Port must be a number between 1024 and 65535.")
            return

        binary = host_binary()
        if binary is None:
            searched = "\n".join("  - " + d for d in candidate_dirs())
            messagebox.showerror(
                "LiveCollab",
                "Companion host not found. Checked:\n" + searched +
                "\n\nUse 'Locate host...' to point to livecollab_host.exe, or " +
                "re-run the LiveCollab installer.")
            return

        args = list(binary) + ["--port", str(port), "--udp", "9595",
                               "--code", self.code, "--name", self.name.get() or "Creator",
                               "--quiet"]
        flags = 0
        if os.name == "nt":
            flags = subprocess.CREATE_NO_WINDOW
        try:
            self.proc = subprocess.Popen(args, creationflags=flags)
        except Exception as e:
            messagebox.showerror("LiveCollab", "Failed to start host: %s" % e)
            return

        self.start_btn.config(state="disabled")
        self.stop_btn.config(state="normal")
        self.status.set("HOSTING - companion running")
        self.info.config(text="Join code: %s\nAddress: %s:%d\n(LAN peers auto-discover)"
                              % (self.code, local_ip(), port))
        self._log("Host started on port %d (code %s)\n" % (port, self.code))

    def stop_host(self):
        if self.proc is not None and self.proc.poll() is None:
            try:
                self.proc.terminate()
                try:
                    self.proc.wait(timeout=3)
                except subprocess.TimeoutExpired:
                    self.proc.kill()
            except Exception:
                pass
        self.proc = None
        self.start_btn.config(state="normal")
        self.stop_btn.config(state="disabled")
        self.status.set("Idle")
        self.info.config(text="")
        self._log("Host stopped.\n")

    # ---------- links ----------
    def open_guide(self):
        guide = os.path.join(here_dir(), "livecollab_guide.html")
        if os.path.exists(guide):
            try:
                os.startfile(guide)  # Windows
                return
            except AttributeError:
                pass
            try:
                subprocess.Popen(["open", guide])  # macOS
                return
            except Exception:
                pass
            try:
                subprocess.Popen(["xdg-open", guide])  # Linux
            except Exception:
                pass

    def open_github(self):
        url = "https://github.com/vexthatguy/LiveCollab"
        try:
            os.startfile(url)
        except AttributeError:
            subprocess.Popen(["open", url]) if sys.platform == "darwin" else None
            if sys.platform != "darwin":
                subprocess.Popen(["xdg-open", url])

    def _log(self, text):
        self.log.config(state="normal")
        self.log.insert("end", text)
        self.log.see("end")
        self.log.config(state="disabled")

    def on_close(self):
        self.stop_host()
        self.root.destroy()


def main():
    root = tk.Tk()
    Manager(root)
    root.mainloop()


if __name__ == "__main__":
    main()
