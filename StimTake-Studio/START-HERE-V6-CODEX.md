# StimTake V6 + Designer — Codex Jump-Off

Date: 2026-08-11
Source baseline: StimTake-Studiov5-end.zip
Status: LOCAL V6 WORKING BUILD — MANUAL LIVE ACCEPTANCE STILL REQUIRED

## What this package does

It introduces two purposeful product surfaces while preserving the existing engine:

1. StimTake Studio 6.0 — model/user front-of-house UI.
2. StimTake Designer 1.0 — developer/content-authoring UI.

The existing Creator Cam / Control Deck code remains present and becomes the
Backstage/manual-tools surface for the V6 Studio process.

## Studio 6.0 source

`StimTakeStudioV6.cs`

The V6 Studio shell:
- shares the existing `StaticServer` instance;
- does not create a second backend;
- starts as the primary front-of-house window;
- exposes the old Control Deck through Backstage;
- displays backend/overlay/OBS process status;
- uses the existing saved Chaturbate model address;
- shows live Chrome Bridge tip events received through the same server;
- uses the shared backend's persisted V6 session counters;
- reads lifetime Top Tippers from the existing authoritative `tippers.tsv`;
- stores model-owned token prices by Show Pack ID + action ID for up to 20 actions;
- validates, installs, activates, and runs bounded Show Pack actions through the preserved engine.

## Designer source

`StimTakeDesigner.cs`

The Designer:
- creates a local workspace;
- authors `pack.json`;
- authors up to 20 `action.json` files;
- creates or imports overlay HTML;
- previews overlay HTML in the default browser;
- authors `theme/theme.json`;
- structurally validates the current draft;
- builds a Show Pack ZIP.

## Implemented and locally verified

- both Windows executables compile and launch;
- one Studio process owns the port-8787 backend;
- Backstage opens/closes without owning or stopping a second backend;
- the backend enforces source, type, locked room, username, positive amount, and persisted event_id;
- wrong-room events change no supporter/session state;
- duplicate event IDs remain suppressed across restart and session reset;
- separate repeated tips from the same user remain separate events;
- session state, lifetime supporter state, tip history, and session history persist locally;
- Show Pack ZIP traversal, malformed JSON, executable payloads, unsafe paths, and action 21 are rejected;
- validated Show Packs install into bounded folders and activate the preserved action engine;
- model pricing persists separately by pack/action identity;
- Designer creates, validates, and exports one-action or 20-action packs;
- Chrome Bridge regression tests remain green.

## Deliberately NOT claimed complete

- A real post-V6 Chaturbate tip has not yet been observed through Studio → action → OBS.
- OBS was not launched for this V6 run; process status does not prove browser-source rendering.
- WebView2 is not added. The current direct `csc.exe`/.NET Framework build has no WebView2 SDK reference; the smallest next step is to add the Microsoft WebView2 WinForms package/runtime without replacing the working Chrome Bridge or current build until proven.
- No clean-machine installer validation was performed.

## Next manual acceptance

1. Start `StimTake-Studio-6.0.exe` and confirm the correct saved model is locked.
2. Import a Designer-built pack, enable one action, and assign a unique token amount.
3. Open the OBS browser source at `http://127.0.0.1:8787/index.html`.
4. Receive one real visible tip for that amount.
5. Confirm one session/lifetime increment, one Last Tipper update, and one action run in OBS.

## Protection

Do not delete the existing Creator Cam engine, Chrome Bridge, action packs, overlays,
manual tools or recovery documentation until replacements are proven.

Do not push without explicit approval.
