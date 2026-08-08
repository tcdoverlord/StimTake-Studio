STIMTAKE V1 - NATIVE FULL LAYOUT PACK IMPORTER
================================================

RESULT
------
The existing JSON Layout Template System now keeps its old JSON workflow AND adds
a separate native ZIP workflow.

Legacy workflow (preserved):
  SAVE TEMPLATE
  LOAD TEMPLATE
  DUPLICATE
  IMPORT JSON
  EXPORT JSON

New full-layout workflow:
  IMPORT LAYOUT PACK ZIP
  EXPORT CURRENT PACK
  RESTORE PREVIOUS LAYOUT

WHY THIS FIXES THE SCREENSHOT ERROR
-----------------------------------
The old IMPORT button intentionally accepts only:
  "type": "creator_cam_layout_template"

The new ZIP importer separately accepts:
  "type": "stimtake-layout-pack"

So a Football/Halloween/etc. full-layout ZIP is no longer sent through the legacy
JSON validator.

FULL PACK CONTENTS
------------------
Required ZIP files:
  layout-pack.json
  module-styles-v3.txt

A FULL pack must contain all 12 movable elements:
  brand
  camera
  goal
  supporters
  last-tipper
  recent
  ticker
  alert
  game-zone
  vip
  dmca
  background

Each line stays compatible with the existing StimTake persistence format:
  module|x|y|scale|opacity|width

SAFETY
------
- ZIP size limit: 16 MB
- archive entry limit: 100
- path traversal rejected
- duplicate required files rejected
- all 12 modules required
- duplicate/unknown modules rejected
- X/Y/scale/opacity/width ranges validated
- current layout is backed up before replacement
- replacement is staged and revalidated
- rollback is attempted if applying fails
- restore backs up the layout it is replacing
- original JSON template importer remains untouched in purpose

TEST ON WINDOWS
---------------
1. Keep the current working StimTake folder untouched.
2. Extract this source candidate to a separate test folder.
3. Run:
     0-BUILD-AND-START-UPDATED-V1.cmd
4. Open LAYOUT + THEMES.
5. Scroll to:
     JSON LAYOUT TEMPLATE + FULL LAYOUT PACK SYSTEM
6. Confirm IMPORT JSON still accepts your Halloween JSON template.
7. Click IMPORT LAYOUT PACK ZIP.
8. Choose Football_Season_Broadcast_Layout.zip.
9. Confirm the 12-element validation preview appears.
10. Approve the import.
11. Confirm the overlay positions update in OBS.
12. Test RESTORE PREVIOUS LAYOUT and confirm the prior positions return.
13. Test EXPORT CURRENT PACK, then import that exported ZIP back.

VALIDATED HERE
--------------
- Modified the complete current CreatorStudioV3.cs from the Overlay Position
  Designer candidate.
- Existing JSON ImportTemplate()/ExportTemplate() methods were preserved.
- CreatorCamPayload.zip was not changed.
- Required new controls/methods are present.
- Static delimiter counts were checked.
- Football_Season_Broadcast_Layout.zip was checked against the same 12-module
  requirements used by the new importer.

NOT VALIDATED HERE
------------------
- Windows .NET Framework compilation
- live WinForms runtime
- live OBS runtime
