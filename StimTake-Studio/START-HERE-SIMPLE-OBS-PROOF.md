# StimTake Studio 6.0 — Simple OBS Proof

This build deliberately removes the old large Creator Cam dashboard from the normal OBS page.

## OBS browser source

Use:

`http://127.0.0.1:8787/index.html`

The page now shows only:

- Top Tippers for the current session
- automatic VIP = highest current-session total
- Last Tipper
- temporary HTML action layer

The rest of the OBS canvas remains transparent.

## Runtime target

```text
real Chaturbate tip
→ Chrome Bridge V3
→ StimTake Studio
→ session supporter total
→ Top Tippers / VIP / Last Tipper
→ one matching enabled action range
→ one HTML overlay temporarily shown in OBS
```

## Placeholder Show Pack

A 20-action test pack is included in:

`Sample-Show-Packs\StimTake-20-Overlay-Placeholder-Show-Pack.zip`

Import it through Studio.

Each HTML overlay is only a numbered placeholder so you can prove which range fired.

## First test

1. Start Studio V6.
2. Load Chrome Bridge V3.
3. Confirm Bridge shows Studio ONLINE and the correct locked model.
4. Add the OBS browser source:
   `http://127.0.0.1:8787/index.html`
5. Import the placeholder Show Pack.
6. Configure:
   - Action 01 = 1–4 tokens ON
   - Action 02 = 5–9 tokens ON
7. Receive one real tip.
8. Confirm:
   - Last Tipper updates
   - Top Tippers updates
   - VIP updates
   - exactly one matching placeholder HTML overlay appears.

Do not call the full action path proven until the real tip triggers the expected placeholder overlay in OBS.
