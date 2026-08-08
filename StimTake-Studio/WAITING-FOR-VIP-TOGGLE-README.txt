STIMTAKE V1 - WAITING FOR VIP ON/OFF BUTTON

PURPOSE
Adds a dedicated StimTake Studio button for the VIP badge shown as WAITING FOR VIP.

BUTTON
WAITING FOR VIP: ON
WAITING FOR VIP: OFF

BEHAVIOR
- ON: the VIP badge is visible.
- OFF: the entire VIP badge is hidden from the overlay.
- Toggling visibility never clears supporter/VIP/session data.
- The setting persists in supporter-overlay-visibility-v1.txt.
- Existing Last Tipper and Last Supporter toggle behavior is preserved.

FILES CHANGED
- CreatorStudioV3.cs
- CreatorCamPayload.zip
  - scripts/overlay.js inside the embedded payload
- SUPPORTER-OVERLAY-TOGGLES-README.txt

WINDOWS BUILD
The Windows executable was not rebuilt in this environment.
Close any running Creator-Cam-Overlay-Kit.exe, then run:

    0-BUILD-AND-START-UPDATED-V1.cmd

EXPECTED MANUAL TEST
1. Start StimTake Studio.
2. Open the page containing LAST TIPPER + LAST SUPPORTER + VIP overlay controls.
3. Confirm WAITING FOR VIP: ON is visible.
4. With the OBS browser overlay visible, click the button once.
5. Confirm the VIP badge disappears and the button changes to WAITING FOR VIP: OFF.
6. Click again.
7. Confirm the VIP badge returns and the button changes to WAITING FOR VIP: ON.
8. Close and reopen Studio/overlay.
9. Confirm the saved ON/OFF state is restored.
10. Confirm Last Tipper and Last Supporter values were not erased.

VALIDATION STATE
- Source patch created.
- JavaScript syntax checked.
- Embedded payload ZIP integrity checked.
- Windows C# compilation/runtime test NOT performed in this environment.
