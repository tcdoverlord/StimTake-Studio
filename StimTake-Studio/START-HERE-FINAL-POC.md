# StimTake Studio 6.0 — Final Proof-of-Concept Candidate (FIX1 Integrated)

## Product flow

```text
Chaturbate model room
        ↓
StimTake Chrome Bridge V3
        ↓
127.0.0.1:8787/api/platform-event
        ↓
StimTake Studio 6.0
        ↓
locked-room + event_id validation
        ↓
session Tips / Tokens
Last Tipper
Top Tippers
VIP = highest session total
        ↓
one enabled matching token range
        ↓
one Show Pack HTML overlay
        ↓
OBS
```

## Normal model app

No Backstage button is exposed in the normal model UI.

The model controls:

- locked room;
- Start New Session / End Session;
- active Show Pack;
- 20 HTML overlay action ranges;
- ON/OFF per action.

StimTake Designer controls the HTML/CSS/JS/assets in each Show Pack.

## Action range examples

```text
01   1–4 tokens      ON
02   5–9 tokens      ON
03   10–19 tokens    OFF
...
20   500+ tokens     ON
```

Rules:

- positive minimum;
- maximum 0 = no maximum;
- enabled ranges cannot overlap;
- gaps are allowed;
- one accepted tip can trigger at most one action;
- disabled/nonmatching tips still update supporter/session state.

## Chrome Bridge

Use `StimTake-Chrome-Bridge-v3.0`.

V2 is preserved for recovery/reference and should be disabled while V3 is loaded.

## Build on Windows

Double-click:

`0-BUILD-AND-START-FINAL-V6.cmd`

or run:

`BUILD-STIMTAKE-V6-AND-DESIGNER.cmd`

Expected outputs:

- `outputs\v6\StimTake-Studio-6.0.exe`
- `outputs\v6\StimTake-Designer-1.0.exe`

## Important evidence boundary

This package was assembled in a non-Windows build environment.

Source/static checks were performed here.
The modified C# source was NOT compiled here because the Windows .NET Framework compiler is unavailable.

Do not call the new range/UI build runtime-proven until the Windows build succeeds and the real live test is performed.


## FIX1 integrated

This full package already contains the corrected `StimTakeStudioV6.cs`.

Restored runtime helpers:

- `RefreshHealth`
- `ServerEventPublished`
- `AddHistory`
- `LoadPersistedHistory`
- `LoadRuntimeState`

You do not need to manually copy the separate FIX1 file into this package.

Build on Windows with:

`0-BUILD-AND-START-FINAL-V6.cmd`
