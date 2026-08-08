STIMTAKE v9 - CHATURBATE TIP BRIDGE CANDIDATE
=============================================
Baseline:
  stimtake_v9_cleaner_layout_ui_buildfix1.zip

Purpose:
  Wire a small Chaturbate TIP connector into the EXISTING StimTake UI.
  No second streaming UI is created.

What the new CONNECTORS tab does:
- Model enters Chaturbate username.
- Model enters Events API token.
- START CHATURBATE launches the bundled Python bridge.
- Token is passed via child-process environment variable.
- Token textbox is cleared immediately after process start.
- Username + bridge settings may be saved locally.
- API token is NOT saved by this patch.
- Real tips enter the existing StimTake platform-event endpoint.
- Existing overlay receives the tip and updates normal StimTake tip behavior.
- Bridge keeps its own JSONL tip log.
- Tip-message requests such as !dice / !wheel are detected.
- Automatic requested-game triggering is an explicit checkbox and defaults OFF.

What was preserved:
- Existing UI
- Actions and 20-Pack workflow
- UI skins
- layout designer and Layout Packs
- rules
- session tools
- existing local connector endpoint
- embedded CreatorCamPayload.zip

Third-party reference:
The supplied chaturbate_poller v5.1.8 was inspected as an MIT reference for
the Chaturbate Events API. The bundled StimTake bridge is a new standard-library
implementation and has no runtime dependency on that package.

Validation performed here:
- Uploaded v9 source archive inspected.
- Bridge Python syntax compiled.
- Bridge sent a normalized fake tip into a mock StimTake endpoint.
- Exact mock payload was verified.
- C# connector UI/method presence statically checked.
- Existing CreatorCamPayload.zip SHA-256 verified unchanged.

Not tested here:
- Windows C# compilation.
- Windows `py` launcher availability.
- Live Chaturbate Events API.
- Real-model API token.
- Live StimTake/OBS behavior.

Windows test order:
1. Keep the currently working StimTake folder untouched.
2. Extract this candidate to a separate folder.
3. Run 0-BUILD-AND-START-UPDATED-V1.cmd.
4. If build fails, send Build-CreatorCam.log and stop there.
5. If build succeeds, open CONNECTORS.
6. Click TEST 25 TOKEN TIP first.
7. Confirm StimTake receives the local fake tip.
8. Enter a real model username/token and click START CHATURBATE.
9. Make a small controlled real tip test.
10. Verify the tipper, amount, goal/tipper UI, and local JSONL log.
11. Leave AUTO-RUN explicit requests OFF until normal tip ingestion is proven.
