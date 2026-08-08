STIMTAKE V1 - LAST TIPPER / LAST SUPPORTER / WAITING FOR VIP VISIBILITY

LAST TIPPER
- Automatically follows the newest tip in the current session.
- Has its own saved ON/OFF overlay toggle.
- OFF hides the overlay only. Tip/session data is preserved.

LAST SUPPORTER
- Separate manual/saved supporter card.
- Has its own saved ON/OFF overlay toggle.
- OFF hides the overlay only. Saved supporter data is preserved.
- CLEAR LAST SUPPORTER clears only the manual Last Supporter card; it does not erase Last Tipper.

WAITING FOR VIP
- The VIP badge/card now has its own saved ON/OFF toggle in StimTake Studio.
- ON shows the VIP badge, including WAITING FOR VIP when no VIP name is available.
- OFF hides the entire VIP badge/card only.
- OFF does not erase supporter, VIP, tip, or session data.
- The button is labeled WAITING FOR VIP: ON / OFF.

All three visibility settings are saved in supporter-overlay-visibility-v1.txt.
Older two-value visibility files remain compatible; WAITING FOR VIP defaults to ON until changed.

BUILD NOTE
- CreatorStudioV3.cs and the embedded CreatorCamPayload.zip were updated.
- The existing compiled EXE in this source package was preserved and was NOT rebuilt here.
- On Windows, close the running Studio and run 0-BUILD-AND-START-UPDATED-V1.cmd to compile the updated EXE.
