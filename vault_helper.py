#!/usr/bin/env python3
"""JSON bridge between the Windows GUI and the Hermes local vault.

Never prints secret values; only metadata and the new handle.
"""
import json
import sys


def emit(obj):
    sys.stdout.write(json.dumps(obj, ensure_ascii=False))
    sys.stdout.flush()
    raise SystemExit(0)


def main():
    if len(sys.argv) < 2:
        emit({"ok": False, "error": "action missing"})

    action = sys.argv[1]
    payload = {}
    if len(sys.argv) >= 3 and sys.argv[2]:
        with open(sys.argv[2], "r", encoding="utf-8") as fh:
            payload = json.load(fh)

    try:
        from agent.vault_store import get_vault_store

        store = get_vault_store()

        if action == "add":
            kind = (payload.get("kind") or "login").strip().lower()
            if kind != "login":
                emit({"ok": False, "error": "GUI ini hanya mendukung kind=login"})

            secret = {
                "identifier_type": (payload.get("identifier_type") or "email").strip(),
                "identifier": (payload.get("identifier") or "").strip(),
                "password": payload.get("password") or "",
            }
            otp = (payload.get("otp_secret") or "").strip()
            if otp:
                secret["otp_secret"] = otp

            meta = store.add_item(
                kind="login",
                label=(payload.get("label") or "").strip(),
                origin=(payload.get("origin") or "").strip(),
                secret=secret,
            )
            emit({"ok": True, "handle": meta.id})

        if action == "list":
            items = []
            for meta in store.list_items():
                items.append({
                    "handle": meta.id,
                    "kind": meta.kind,
                    "label": meta.label,
                    "identifier": meta.identifier or "",
                    "origin": meta.origin or "",
                })
            emit({"ok": True, "items": items})

        if action == "rm":
            handle = (payload.get("handle") or "").strip()
            ok = bool(handle) and store.remove_item(handle)
            emit({"ok": ok, "error": "" if ok else "Item tidak ditemukan"})

        if action == "key":
            emit({"ok": True, "key": store.key_location()})

        emit({"ok": False, "error": "unknown action: " + action})
    except Exception as exc:  # noqa: BLE001 - the GUI needs the message
        emit({"ok": False, "error": str(exc)})


if __name__ == "__main__":
    main()
