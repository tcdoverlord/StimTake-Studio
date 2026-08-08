STIMTAKE V1 - CLEANER LAYOUT UI
================================

Purpose
-------
Keep every existing layout capability while making the UI easier to understand.

What changed
------------
The combined layout/template area is now visually separated into:

1. JSON TEMPLATE
   - SAVE JSON
   - LOAD JSON
   - DUPLICATE
   - IMPORT JSON TEMPLATE
   - EXPORT JSON TEMPLATE

2. FULL LAYOUT PACK
   - IMPORT LAYOUT PACK ZIP
   - EXPORT CURRENT LAYOUT PACK
   - RESTORE LAST LAYOUT

The large JSON editor remains available for advanced users.

Why
---
The old UI had multiple Import/Export buttons close together and it was easy to
send a Layout Pack ZIP into the legacy JSON-template importer.

This redesign keeps BOTH workflows but makes the package type explicit.

Preserved
---------
- Existing JSON template format
- Existing full Layout Pack ZIP format
- Overlay Position Designer
- Dual Action Deck loader
- Waiting for VIP controls
- Protected-live-broadcast / DMCA controls
- CreatorCamPayload.zip unchanged

Validation
----------
- Complete current CreatorStudioV3.cs modified in place.
- No importer/exporter logic removed.
- Only BuildTemplateGroup UI layout/labels changed.
- CreatorCamPayload.zip hash verified unchanged.
- Basic delimiter balance checked.

Not validated here
------------------
- Windows .NET Framework compile
- WinForms DPI/runtime layout
- Live OBS runtime
