#!/usr/bin/env python3
"""
StimTake Chaturbate Bridge v1.0

Purpose:
- Listen to a single broadcaster's Chaturbate Events API.
- Forward TIP events to the model's local StimTake Studio instance.
- Keep a local JSONL tip log with username, amount, message, request, and event id.
- Optionally trigger dice/wheel only when the model explicitly enables request mode.

The Chaturbate API token is read from an environment variable or secure console
prompt. It is never written to config or logs by this program.
"""

from __future__ import annotations

import argparse
import getpass
import json
import os
from pathlib import Path
import re
import sys
import time
from typing import Any
import urllib.error
import urllib.parse
import urllib.request

VERSION = "1.0.0"
DEFAULT_STIMTAKE_ENDPOINT = "http://127.0.0.1:8787/api/platform-event"
PROD_HOST = "eventsapi.chaturbate.com"
TESTBED_HOST = "events.testbed.cb.dev"

_REQUEST_DICE = re.compile(r"(?:^|\s)(?:!dice|!roll|roll\s+dice|roll\s+the\s+dice)(?:\s|$)", re.I)
_REQUEST_WHEEL = re.compile(r"(?:^|\s)(?:!wheel|!spin|spin\s+wheel|spin\s+the\s+wheel)(?:\s|$)", re.I)


def local_data_root() -> Path:
    base = os.environ.get("LOCALAPPDATA")
    if base:
        return Path(base) / "StimTakeStudio" / "ChaturbateBridge"
    return Path.home() / ".stimtake" / "chaturbate-bridge"


def safe_text(value: Any, limit: int) -> str:
    text = str(value or "").replace("\r", " ").replace("\n", " ").strip()
    return text[:limit]


def detect_request(message: str) -> str:
    if _REQUEST_DICE.search(message):
        return "dice"
    if _REQUEST_WHEEL.search(message):
        return "wheel"
    return ""


def log_tip(payload: dict[str, Any], log_dir: Path) -> None:
    log_dir.mkdir(parents=True, exist_ok=True)
    path = log_dir / (time.strftime("%Y-%m-%d") + "-tips.jsonl")
    record = {
        "time": payload.get("timestamp"),
        "username": payload.get("username"),
        "amount": payload.get("amount"),
        "message": payload.get("message"),
        "request": payload.get("request"),
        "event_id": payload.get("event_id"),
        "source": "chaturbate",
    }
    with path.open("a", encoding="utf-8") as handle:
        handle.write(json.dumps(record, ensure_ascii=False) + "\n")


def send_stimtake(endpoint: str, payload: dict[str, Any]) -> None:
    data = json.dumps(payload, ensure_ascii=False, separators=(",", ":"))
    url = endpoint + "?data=" + urllib.parse.quote(data, safe="")
    request = urllib.request.Request(
        url,
        headers={"User-Agent": f"StimTake-Chaturbate-Bridge/{VERSION}"},
        method="GET",
    )
    with urllib.request.urlopen(request, timeout=5) as response:
        # StimTake currently returns 204 No Content for accepted events.
        if response.status not in (200, 204):
            raise RuntimeError(f"StimTake returned HTTP {response.status}")


def allowed_next_url(url: str, testbed: bool) -> bool:
    parsed = urllib.parse.urlparse(url)
    expected = TESTBED_HOST if testbed else PROD_HOST
    return parsed.scheme == "https" and parsed.hostname == expected


def fetch_json(url: str) -> dict[str, Any]:
    request = urllib.request.Request(
        url,
        headers={
            "User-Agent": f"StimTake-Chaturbate-Bridge/{VERSION}",
            "Accept": "application/json",
        },
        method="GET",
    )
    with urllib.request.urlopen(request, timeout=310) as response:
        if response.status != 200:
            raise RuntimeError(f"Chaturbate returned HTTP {response.status}")
        return json.loads(response.read().decode("utf-8"))


def normalize_tip(event: dict[str, Any]) -> dict[str, Any] | None:
    if str(event.get("method", "")) != "tip":
        return None
    obj = event.get("object") or {}
    user = obj.get("user") or {}
    tip = obj.get("tip") or {}

    username = safe_text(user.get("username") or "Anonymous", 64)
    try:
        amount = int(tip.get("tokens") or 0)
    except (TypeError, ValueError):
        amount = 0
    if amount <= 0:
        return None

    message = safe_text(tip.get("message"), 240)
    request = detect_request(message)

    return {
        "type": "tip",
        "username": username,
        "amount": amount,
        "message": message,
        "request": request,
        "source": "chaturbate",
        "event_id": safe_text(event.get("id"), 160),
        "timestamp": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
    }


def maybe_trigger_requested_game(
    endpoint: str,
    tip: dict[str, Any],
    mode: str,
    dice_min: int,
    wheel_min: int,
) -> None:
    if mode != "trigger":
        return

    request = tip.get("request")
    amount = int(tip.get("amount") or 0)
    username = str(tip.get("username") or "")

    if request == "dice" and amount >= dice_min:
        send_stimtake(
            endpoint,
            {
                "type": "dice",
                "username": username,
                "source": "chaturbate-tip-request",
                "tip_amount": amount,
                "count": 2,
                "sides": 6,
            },
        )
    elif request == "wheel" and amount >= wheel_min:
        send_stimtake(
            endpoint,
            {
                "type": "wheel",
                "username": username,
                "source": "chaturbate-tip-request",
                "tip_amount": amount,
                "name": "MAIN",
            },
        )


def run_test_tip(args: argparse.Namespace) -> int:
    tip = {
        "type": "tip",
        "username": safe_text(args.test_username, 64) or "BridgeTest",
        "amount": max(1, args.test_amount),
        "message": safe_text(args.test_message, 240),
        "request": detect_request(args.test_message),
        "source": "stimtake-bridge-test",
        "event_id": "local-test-" + str(int(time.time() * 1000)),
        "timestamp": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
    }
    send_stimtake(args.endpoint, tip)
    print(f"TEST TIP SENT -> {tip['username']} / {tip['amount']} tokens / request={tip['request'] or 'none'}")
    return 0


def poll(args: argparse.Namespace) -> int:
    username = safe_text(
        args.username
        or os.environ.get("STIMTAKE_CB_USERNAME")
        or os.environ.get("CB_USERNAME"),
        80,
    )
    if not username:
        username = safe_text(input("Chaturbate username: "), 80)
    if not username:
        print("ERROR: Chaturbate username is required.", file=sys.stderr)
        return 2

    token = (
        args.token
        or os.environ.get("STIMTAKE_CB_TOKEN")
        or os.environ.get("CB_TOKEN")
        or ""
    ).strip()
    if not token:
        token = getpass.getpass("Chaturbate Events API token (not saved): ").strip()
    if not token:
        print("ERROR: Chaturbate Events API token is required.", file=sys.stderr)
        return 2

    host = TESTBED_HOST if args.testbed else PROD_HOST
    base_url = (
        f"https://{host}/events/"
        f"{urllib.parse.quote(username, safe='')}/"
        f"{urllib.parse.quote(token, safe='')}/"
        f"?timeout={max(1, min(30, args.api_timeout))}"
    )

    log_dir = local_data_root() / "logs"
    next_url: str | None = base_url
    seen_ids: list[str] = []
    seen_set: set[str] = set()
    backoff_seconds = 2

    print(f"StimTake Chaturbate Bridge v{VERSION}")
    print(f"Model: {username}")
    print(f"StimTake: {args.endpoint}")
    print(f"Tip log: {log_dir}")
    print(f"Game request mode: {args.request_mode}")
    print("Token is held in memory only. Press Ctrl+C to stop.")

    while True:
        try:
            if not next_url or not allowed_next_url(next_url, args.testbed):
                next_url = base_url

            body = fetch_json(next_url)
            events = body.get("events") or []

            for event in events:
                event_id = safe_text(event.get("id"), 160)
                if event_id and event_id in seen_set:
                    continue

                tip = normalize_tip(event)
                if tip:
                    send_stimtake(args.endpoint, tip)
                    log_tip(tip, log_dir)
                    print(
                        f"TIP -> {tip['username']} / {tip['amount']} tokens"
                        + (f" / request={tip['request']}" if tip["request"] else "")
                    )
                    maybe_trigger_requested_game(
                        args.endpoint, tip, args.request_mode, args.dice_min, args.wheel_min
                    )

                if event_id:
                    seen_ids.append(event_id)
                    seen_set.add(event_id)
                    if len(seen_ids) > 1000:
                        old = seen_ids.pop(0)
                        seen_set.discard(old)

            candidate = body.get("nextUrl") or body.get("next_url")
            next_url = candidate if isinstance(candidate, str) and allowed_next_url(candidate, args.testbed) else base_url
            backoff_seconds = 2

        except KeyboardInterrupt:
            print("\nConnector stopped.")
            return 0
        except urllib.error.HTTPError as error:
            # Never print request URLs because the Events API URL contains the token.
            if error.code in (401, 403):
                print("ERROR: Chaturbate rejected the Events API credentials.", file=sys.stderr)
                return 3
            print(f"Chaturbate HTTP error {error.code}; retrying in {backoff_seconds}s.", file=sys.stderr)
        except urllib.error.URLError as error:
            print(f"Network error; retrying in {backoff_seconds}s: {safe_text(error.reason, 120)}", file=sys.stderr)
        except Exception as error:
            print(f"Connector error; retrying in {backoff_seconds}s: {safe_text(error, 160)}", file=sys.stderr)

        time.sleep(backoff_seconds)
        backoff_seconds = min(30, backoff_seconds * 2)


def parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(description="Purpose-built Chaturbate tip bridge for StimTake Studio.")
    p.add_argument("--username", default="")
    p.add_argument("--token", default="", help="Not recommended: prefer STIMTAKE_CB_TOKEN or secure prompt.")
    p.add_argument("--endpoint", default=DEFAULT_STIMTAKE_ENDPOINT)
    p.add_argument("--api-timeout", type=int, default=10)
    p.add_argument("--testbed", action="store_true")
    p.add_argument("--request-mode", choices=("off", "detect", "trigger"), default="detect")
    p.add_argument("--dice-min", type=int, default=1)
    p.add_argument("--wheel-min", type=int, default=1)

    p.add_argument("--test-tip", action="store_true")
    p.add_argument("--test-username", default="BridgeTest")
    p.add_argument("--test-amount", type=int, default=25)
    p.add_argument("--test-message", default="!dice")
    return p


def main() -> int:
    args = parser().parse_args()
    if args.test_tip:
        return run_test_tip(args)
    return poll(args)


if __name__ == "__main__":
    raise SystemExit(main())
