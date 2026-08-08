STIMTAKE V1 - DUAL ACTION DECK LOADER
=======================================

PURPOSE
-------
Keep BOTH Action Deck workflows:

RETRO MODE
  Replace one Action slot at a time with BROWSE / REPLACE ONE HTML.

20-PACK MODE
  Import one validated ZIP and replace all Action slots 1-20 together.

Nothing removes the original single-slot workflow.

NEW UI
------
ACTION DECK now includes:

RETRO MODE
  [slot 1-20]
  [overlay name]
  [BROWSE / REPLACE ONE HTML]

20-PACK MODE
  [IMPORT / REPLACE 20-PACK ZIP]
  [RESTORE PREVIOUS 20-PACK]

Plus the existing:
  [STOP ALL]
  [CLEAR SLOT]
  [REFRESH]

20-PACK PACKAGE FORMAT
----------------------
A full pack must contain exactly one overlay for every slot 1-20.

Accepted folder patterns:
  action-01-name/
  action-02-name/
  ...
  action-20-name/

or:
  slot-01/
  ...
  slot-20/

Each slot folder must contain:
  overlay.html

module.json is optional for import. If present, its "name" is used as the Action
button description/tooltip. StimTake writes its own normalized slot module.json
after validation.

Local supported assets may include:
  .json .html .htm .css .js .mjs
  .png .jpg .jpeg .gif .webp .svg
  .wav .mp3 .ogg
  .txt
  .woff .woff2 .ttf .otf
  .mp4 .webm

SAFETY / RECOVERY
-----------------
The full ZIP is validated and staged before any working Action slot is replaced.

Before a 20-pack replacement, StimTake creates a recovery snapshot under:
  backupscripts_action_v4_pack_history/

If an install fails after replacement begins, StimTake attempts to roll the deck
back from that snapshot.

RESTORE PREVIOUS 20-PACK restores the newest saved Action Deck snapshot and
preserves the deck being replaced before restoration.

The old one-slot workflow keeps its existing managed slot backup behavior.

LIMITS
------
ZIP: 300 MB maximum
Archive entries: 1200 maximum
Single extracted asset: 32 MB maximum
Expanded pack: 500 MB maximum

VALIDATION COMPLETED HERE
-------------------------
- CreatorStudioV3.cs braces, brackets, and parentheses balanced after patch.
- Both Action Deck modes remain in source.
- 20-pack discovery logic was checked against:
    Obsidian_Pack_Couple_Showtime_v1.zip
  and detected slots 1 through 20.
- Existing Waiting for VIP and DMCA control work remains in the source baseline.
- No live Windows/.NET Framework compilation was performed here.
- No live StimTake / OBS runtime test was performed here.

WINDOWS TEST
------------
1. Keep your currently working StimTake folder untouched.
2. Extract this candidate to a separate folder.
3. Close the running StimTake executable.
4. Run:
     0-BUILD-AND-START-UPDATED-V1.cmd
5. Open ACTION DECK.
6. Confirm RETRO MODE still replaces only the selected slot.
7. Test IMPORT / REPLACE 20-PACK ZIP with:
     Obsidian_Pack_Couple_Showtime_v1.zip
8. Confirm slots 1-20 populate.
9. Trigger several Action buttons.
10. Test RESTORE PREVIOUS 20-PACK.
11. Confirm the earlier Action Deck returns.

RECOVERY
--------
If the candidate does not behave correctly, close it and return to your untouched
working StimTake folder.
