# StimTake Studio — Codex Handoff Safe Point
Date: 2026-08-11
Classification: Local source handoff / recovery checkpoint
Release status: NOT A RELEASE

## Purpose

This package combines the latest supplied StimTake Studio candidate with the
working StimTake Chrome Bridge source and the updated project README so the
project can be preserved before Codex continues development.

## Proven / supported by current evidence

- StimTake Chrome Bridge attaches to the live Chaturbate message list.
- The Bridge has detected a real visible Chaturbate tip with the correct
  username and token amount exactly once.
- The Bridge is receiver-only.
- The Bridge sends local platform events to:
  http://127.0.0.1:8787/api/platform-event
- StimTake Studio contains the localhost platform-event receiver.
- The model-facing Connectors simplification work is included.
- Existing Studio/Creator Cam source, assets, action packages, overlay payload,
  and build scripts from the supplied candidate are preserved.

## Important unresolved / NOT proven

- Top Tippers / Fans restore is still showing stale TestViewer state in OBS.
  Do not treat the included TestViewer repair attempts as proven working.
- The final single-process architecture where StimTake owns the backend
  automatically is planned, not implemented/proven.
- WebView2 locked model monitoring is planned, not implemented/proven.
- Saved-model room enforcement is not yet fully proven end-to-end.
- Real tip -> complete Studio supporter state -> OBS overlay path still needs
  end-to-end validation.
- No clean-machine or installer validation has been completed.
- No Git commit is created by this package.

## Intended next architecture

StimTake Studio should become the authoritative owner of:
- local backend lifecycle;
- port 8787 event receiver;
- supporter/session state;
- model lock;
- overlays;
- optional Backstage window;
- future locked WebView2 model monitor.

Chrome Bridge should remain preserved during that transition as the proven
real-tip detector/fallback.

## Codex protection instructions

Before changing D:\StimTake-Studio:

1. Confirm repository root.
2. Run git status.
3. Inspect recent history.
4. Confirm tag: StimTake-V1-75pct-Known-Good-2026-08-08.
5. Preserve all unrelated tracked and untracked work.
6. Do not reset, clean, delete, force checkout, or overwrite unrelated files.
7. Do not push.
8. Make the smallest responsible change.
9. Keep Chrome Bridge receiver-only.
10. Keep Windows/browser protections enabled.
11. Do not capture passwords, cookies, API tokens, or browser session secrets.
12. Do not claim the Top Tippers/TestViewer bug fixed until OBS proves it.
13. Create a local Git checkpoint only after inspecting the diff and validating
    the intended files.

## First Codex investigation

Trace the authoritative supporter-state path causing TestViewer to remain in
the OBS Top Tippers panel after a real backup is loaded. Inspect the running
backend/overlay state, persisted data files, payload extraction/update behavior,
and OBS browser source before making another speculative filter.

Do not begin WebView2 work until this state ownership issue is understood or
explicitly checkpointed as a separate known defect.
