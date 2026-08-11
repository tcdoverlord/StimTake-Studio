# StimTake Chrome Bridge v2.0

## What v2 does

StimTake Chrome Bridge v2 is a receiver-only Chrome extension for the current
StimTake Studio Chaturbate integration experiment.

Its job is:

```text
Chaturbate room in Chrome
        ↓
watch rendered chat DOM
        ↓
detect a received tip
        ↓
username + token amount + optional message
        ↓
127.0.0.1:8787
        ↓
StimTake Studio
```

It does not send tips, purchase tokens, request payment information, request a
Chaturbate password, or request a Chaturbate API token.

## What changed from v0.1 / v0.2

v2 adds much more diagnostic information so we can tell exactly where a test
fails.

The popup now shows:

- whether the page observer is WATCHING or WAITING;
- the Chaturbate room name;
- the current page title and URL;
- how many DOM mutations the observer has seen;
- how many tip-like candidates were seen;
- how many tips were delivered to StimTake;
- the complete last normalized tip event;
- the last tip-like DOM candidate;
- the last localhost delivery time;
- the last StimTake delivery error;
- a Copy Diagnostics button.

v2 also explicitly supports the live tip format observed during development:

```text
higeva3943
tipped 25 tokens
```

The username and `tipped N tokens` text may be separate sibling or nested
elements. v2 searches nearby DOM nodes and parent containers to reconstruct
that tip.

## Install

1. Extract `StimTake-Chrome-Bridge-v2.0.zip`.
2. Open Chrome.
3. Open `chrome://extensions`.
4. Turn on Developer mode.
5. Disable or remove the older StimTake Chrome Bridge version.
6. Click Load unpacked.
7. Select the extracted `StimTake-Chrome-Bridge-v2.0` folder.
8. Open a Chaturbate room.
9. Refresh the Chaturbate room once.
10. Click the StimTake extension icon.

The popup should report:

```text
Page Observer: WATCHING
Room: model_name
```

The DOM mutations counter should increase as new chat content arrives.

## StimTake Studio local API contract

The extension forwards only real tips reconstructed from the Chaturbate page.

Current local endpoint:

```text
GET http://127.0.0.1:8787/api/platform-event?data=<URL-encoded JSON>
```

The decoded JSON event has this shape:

```json
{
  "type": "tip",
  "username": "viewer_name",
  "amount": 25,
  "message": "",
  "request": "",
  "source": "chaturbate-browser",
  "room": "model_name",
  "event_id": "dom-...",
  "timestamp": "2026-08-10T00:00:00.000Z"
}
```

StimTake Studio should accept the event only on localhost, decode `data`, require
`type == "tip"`, validate a positive integer `amount`, and use `event_id` as the
idempotency key so a retried delivery does not become a second tip.

Any HTTP 2xx response (including 204) is treated by the extension as accepted.
A failed request remains visible in the popup as `Last Error`.

There is intentionally no synthetic/test-tip command in the extension.

## Test a real room

Leave the Chaturbate room open and wait for a real public tip.

There are three useful outcomes.

### Delivered Tips increases

The parser recognized the real tip and forwarded it to StimTake.

### Diagnostic Candidates increases

The observer saw tip-like Chaturbate DOM text, but the parser still needs an
adjustment.

Click COPY DIAGNOSTICS and send the copied diagnostic object for analysis.

### DOM mutations increase but both tip counters stay zero

The observer is attached and receiving DOM changes, but Chaturbate is rendering
tip alerts differently from the text/structure we currently recognize.

The next debugging step is to inspect one actual tip node.

## Console

Press F12 and open Console.

Filter for:

`StimTake Bridge`

Expected startup:

```text
[StimTake Bridge] v2.0 loaded. Receiver-only mode.
[StimTake Bridge] watching Chaturbate chat DOM:
```

Recognized tip:

```text
[StimTake Bridge] TIP DETECTED:
[StimTake Bridge] delivered to StimTake.
```

Unparsed tip-like structure:

```text
[StimTake Bridge] candidate DOM node:
```

## Security boundary

The extension only needs storage plus host access to Chaturbate pages and
StimTake's localhost endpoint.

Chrome's extension model supports content scripts reading the page DOM and
message passing between content scripts and the extension service worker.
The localhost request is made by the extension service worker using explicit
host permission.

The browser-side parser has been proven against a real visible Chaturbate tip.
The remaining integration gate is StimTake Studio consuming the localhost event
contract above and proving one real tip reaches Studio exactly once.
