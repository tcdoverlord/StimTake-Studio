STIMTAKE V1 - OVERLAY POSITION DESIGNER
=======================================

Goal
----
Make the 1920x1080 OBS overlay highly user-customizable without removing any
existing layout, theme, Action Deck, DMCA, VIP, session, or template features.

What changed
------------
LAYOUT + THEMES now contains a user-facing OVERLAY POSITION DESIGNER.

Selectable elements:
- Brand Panel
- Camera Frame
- Token Goal
- Top Tippers / Fans
- Last Tipper
- Recent Supporter
- Tip Ticker
- Alert Display
- Game Overlay Zone
- VIP Badge
- DMCA Watermark
- Background

Controls:
- X offset in pixels: -1500 to +1500
- Y offset in pixels: -900 to +900
- Nudge step: 1 to 500 pixels
- LEFT / RIGHT / UP / DOWN buttons
- Size: 25% to 300%
- Opacity: 0% to 100%
- Width: 0/default to 100%
- APPLY / SAVE
- RESET SELECTED

Examples
--------
X = -110  => move left 110 pixels
X =  110  => move right 110 pixels
Y =  -80  => move up 80 pixels
Y =   80  => move down 80 pixels

The nudge arrows immediately save/apply the new position.

Compatibility
-------------
The underlying overlay engine already supported per-module:
x, y, scale, opacity, and width.

This candidate exposes that existing engine in a friendlier UI.

The existing module-styles-v3.txt format is PRESERVED:
module|x|y|scale|opacity|width

Scale and opacity are still stored as decimals internally (for example 1.0 and
0.85), while the UI now presents them as familiar percentages (100% and 85%).

Existing saved positions therefore remain compatible.

Protection
----------
The baseline ZIP was not modified.
CreatorCamPayload.zip was not changed.
The Action Deck dual-loader work is preserved.
The Waiting for VIP work is preserved.
The protected-live-broadcast / DMCA controls are preserved.

Windows test
------------
1. Extract this candidate to a separate folder.
2. Close the running StimTake.
3. Run:
     0-BUILD-AND-START-UPDATED-V1.cmd
4. Open LAYOUT + THEMES.
5. Choose Token Goal.
6. Set X = -110 and click APPLY / SAVE.
7. Confirm the Token Goal moves left 110 px in OBS.
8. Try the arrow nudge buttons.
9. Change to Brand Panel, Camera Frame, Top Tippers, Tip Ticker, and Background.
10. Confirm each remembers its own values.
11. Restart StimTake and confirm saved positions return.
12. Test RESET SELECTED on one element.

Validation performed here
-------------------------
- The complete current CreatorStudioV3.cs was modified rather than reconstructed.
- Existing saved-style file format was preserved.
- C# delimiter balance was checked after modification.
- Required new methods and controls were checked for presence.
- CreatorCamPayload.zip hash was verified unchanged from the baseline candidate.

Not performed here
------------------
- Windows .NET Framework compilation
- WinForms visual/DPI test
- Live OBS browser-source test
