STIMTAKE V1 - PROTECTED LIVE BROADCAST WATERMARK CONTROLS
============================================================

PURPOSE
-------
Adds model-specific controls for the built-in moving DMCA/protected-live-broadcast
watermark without removing the existing optional custom HTML upload feature.

WHAT CHANGED
------------
1. UI SKINS + HTML > DMCA area now includes:
   - Model / username field
   - Opacity selector: 10% through 95%
   - SAVE + SHOW
   - SAVE
   - HIDE
   - Live saved-status text

2. The protected watermark uses:
   Title: PROTECTED LIVE BROADCAST
   Username: @<model name entered in the UI>
   Tagline: Unauthorized recording or redistribution is prohibited

3. The Dashboard moving-watermark controls now also include an opacity selector.

4. Opacity is persisted in moving-watermark-v4.txt as a fifth tab-separated value.
   Existing four-value settings files remain compatible and default to 82% opacity.

5. The existing custom DMCA HTML upload/replace/on/off/delete controls are preserved
   as a separate optional feature.

IMPORTANT LIMITATION
--------------------
This patch controls the built-in StimTake moving watermark. It does NOT rewrite text
inside an already-uploaded third-party/custom DMCA overlay.html. If a custom HTML
overlay contains a hardcoded username such as obsidian_stallion, that exact HTML file
must be edited separately.

BUILD / TEST
------------
This is a SOURCE PATCH candidate. The pre-existing EXE in the folder was not rebuilt
in this environment.

On Windows:
1. Close the running StimTake / Creator Cam executable.
2. Extract this ZIP to a separate test folder.
3. Run:
      0-BUILD-AND-START-UPDATED-V1.cmd
4. Open UI SKINS + HTML.
5. In PROTECTED LIVE BROADCAST WATERMARK:
      - enter a model name
      - choose an opacity
      - click SAVE + SHOW
6. Confirm the OBS overlay updates within a few seconds.
7. Click HIDE and confirm it disappears.
8. Re-open StimTake and verify the name, opacity, and visibility state persist.

RECOVERY
--------
Keep your previous working StimTake package unchanged. If this candidate does not
behave correctly, close it and return to that package.

VALIDATION COMPLETED HERE
-------------------------
- CreatorStudioV3.cs braces/parentheses/brackets are balanced after patching.
- All SaveMovingWatermarkSettings call sites were updated to the new opacity-aware
  signature.
- Existing custom DMCA HTML controls remain in source.
- CreatorCamPayload.zip was left byte-for-byte unchanged.
- Live Windows/.NET Framework compilation and OBS runtime testing were NOT performed.
