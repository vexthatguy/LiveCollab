#!/usr/bin/env python3
"""
LiveCollab standalone host companion (relay broker).

This process exists ONLY while you are hosting. Until then it is not started
at all, so it consumes zero CPU/RAM in your project.

What it does:
  - Listens on TCP (default 9501) for the Unity editor(s) and guests.
  - The first peer that identifies as the OWNER (the editor that pressed
    "Start hosting") controls the session.
  - Guest messages are forwarded to the owner; owner messages fan out to all
    guests; presence messages fan out to everyone.
  - Replies to guests with a WELCOME (uid + color) and stamps their presence
    packets with that id, so collaborators stay visually consistent.
  - Announces itself with a UDP beacon so LAN clients auto-discover it.

Usage:
  python livecollab_host.py [--port 9501] [--udp 9595]
                            [--code ABCD] [--name "Host"] [--quiet]

No third-party dependencies — standard library only (Python 3.6+).
"""

import argparse
import json
import socket
import struct
import sys
import threading
import time

MAGIC = "LIVECOLAB1"
DEFAULT_PORT = 9501
DEFAULT_UDP = 9595
MAX_PEERS = 32
MAX_FRAME = 16 * 1024 * 1024

# mirrors Unity's HostSession palette
PALETTE = [
    "#22C4E8", "#FCCC33", "#F273C0", "#8CD959", "#F2994D", "#A68CF2",
]


def pack_msg(t, d):
    return json.dumps({"t": t, "d": d}).encode("utf-8")


def unpack_msg(raw):
    try:
        obj = json.loads(raw.decode("utf-8"))
        return obj.get("t", ""), obj.get("d", "")
    except Exception:
        return None, None


class Peer:
    __slots__ = ("sock", "addr", "name", "role", "uid", "color", "alive",
                 "rbuf", "lock")

    def __init__(self, sock, addr):
        self.sock = sock
        self.addr = addr
        self.name = "?"
        self.role = None          # "owner" | "client" | None before HELLO
        self.uid = 0
        self.color = PALETTE[0]
        self.alive = True
        self.rbuf = b""
        self.lock = threading.Lock()

    def send(self, frame):
        if not self.alive:
            return False
        try:
            with self.lock:
                self.sock.sendall(struct.pack("<i", len(frame)) + frame)
            return True
        except Exception:
            return False


class Relay:
    def __init__(self, args):
        self.args = args
        self.peers = []
        self.peers_lock = threading.Lock()
        self.owner = None
        self.next_uid = 1
        self.udp_sock = None

    # ---------------- TCP ----------------

    def serve_tcp(self):
        srv = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        srv.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        try:
            srv.bind(("0.0.0.0", self.args.port))
        except OSError as e:
            print("[LiveCollab] ERROR: cannot bind port %d: %s" % (self.args.port, e), flush=True)
            return
        srv.listen(MAX_PEERS)
        print("[LiveCollab] TCP listening on :%d" % self.args.port, flush=True)
        while True:
            try:
                sock, addr = srv.accept()
            except OSError:
                break
            try:
                sock.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
            except OSError:
                pass
            p = Peer(sock, addr)
            with self.peers_lock:
                if len(self.peers) >= MAX_PEERS:
                    sock.close()
                    continue
                self.peers.append(p)
            threading.Thread(target=self.handle_peer, args=(p,), daemon=True).start()

    def handle_peer(self, p):
        try:
            while p.alive:
                data = p.sock.recv(65536)
                if not data:
                    break
                p.rbuf += data
                while True:
                    if len(p.rbuf) < 4:
                        break
                    (n,) = struct.unpack("<i", p.rbuf[:4])
                    if n < 0 or n > MAX_FRAME:
                        raise ValueError("bad frame length")
                    if len(p.rbuf) < 4 + n:
                        break
                    raw = p.rbuf[4:4 + n]
                    p.rbuf = p.rbuf[4 + n:]
                    self.route(p, raw)
        except Exception:
            pass
        finally:
            self.drop(p)

    def route(self, p, raw):
        t, d = unpack_msg(raw)
        if t is None:
            return

        if t == "h":  # HELLO
            try:
                hello = json.loads(d)
                p.name = hello.get("name", "?")
                p.role = hello.get("role") or "client"
            except Exception:
                p.role = "client"
            if p.role == "owner":
                with self.peers_lock:
                    if self.owner is None or not self.owner.alive:
                        self.owner = p
                print("[LiveCollab] owner online: %s" % p.name, flush=True)
            else:
                with self.peers_lock:
                    p.uid = self.next_uid
                    self.next_uid += 1
                p.color = PALETTE[(p.uid - 1) % len(PALETTE)]
                welcome = {"uid": p.uid, "color": p.color,
                           "tickMs": 100}
                p.send(pack_msg("w", json.dumps(welcome)))
                print("[LiveCollab] guest joined: %s (%s)" % (p.name, p.addr[0]), flush=True)
            return

        if t == "p":  # presence
            try:
                pres = json.loads(d)
            except Exception:
                pres = None
            if pres is not None and p.role != "owner":
                pres["uid"] = p.uid
                pres["color"] = p.color
                d = json.dumps(pres)
            raw = pack_msg("p", d)
            with self.peers_lock:
                targets = [q for q in self.peers if q is not p and q.alive]
            for q in targets:
                q.send(raw)
            return

        if p.role == "owner":
            # owner -> all guests
            with self.peers_lock:
                targets = [q for q in self.peers
                           if q is not p and q.alive and q.role != "owner"]
            for q in targets:
                q.send(raw)
        else:
            # guest -> owner only
            o = None
            with self.peers_lock:
                o = self.owner
            if o is not None and o.alive:
                o.send(raw)

    def drop(self, p):
        with self.peers_lock:
            if p in self.peers:
                self.peers.remove(p)
            if self.owner is p:
                self.owner = None
        p.alive = False
        try:
            p.sock.close()
        except Exception:
            pass
        if p.role == "owner":
            print("[LiveCollab] owner offline — room awaits a new owner", flush=True)
        elif p.role == "client":
            print("[LiveCollab] guest left: %s" % p.name, flush=True)

    # ---------------- UDP beacon ----------------

    def serve_udp(self):
        try:
            u = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
            u.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1)
            u.bind(("0.0.0.0", self.args.udp))
        except OSError as e:
            print("[LiveCollab] UDP beacon disabled: %s" % e, flush=True)
            return
        self.udp_sock = u
        # format: MAGIC|name|ip|port|code  (ip left empty -> clients use sender addr)
        payload = ("%s|%s|%s|%d|%s" % (MAGIC, self.args.name, "", self.args.port, self.args.code)).encode("utf-8")
        while True:
            try:
                u.sendto(payload, ("255.255.255.255", self.args.udp))
            except OSError:
                pass
            time.sleep(2)


def main():
    ap = argparse.ArgumentParser(description="LiveCollab standalone host relay")
    ap.add_argument("--port", type=int, default=DEFAULT_PORT)
    ap.add_argument("--udp", type=int, default=DEFAULT_UDP)
    ap.add_argument("--code", default="")
    ap.add_argument("--name", default="Host")
    ap.add_argument("--quiet", action="store_true")
    args = ap.parse_args()

    relay = Relay(args)
    threading.Thread(target=relay.serve_tcp, daemon=True).start()
    threading.Thread(target=relay.serve_udp, daemon=True).start()

    if not args.quiet:
        print("[LiveCollab] companion running (name '%s', code %s)" % (args.name, args.code or "N/A"), flush=True)
        print("[LiveCollab] press Ctrl+C to stop", flush=True)

    try:
        while True:
            time.sleep(3600)
    except KeyboardInterrupt:
        if not args.quiet:
            print("\n[LiveCollab] stopping", flush=True)

    with relay.peers_lock:
        for p in relay.peers:
            p.alive = False
            try:
                p.sock.close()
            except Exception:
                pass


if __name__ == "__main__":
    sys.exit(main())
