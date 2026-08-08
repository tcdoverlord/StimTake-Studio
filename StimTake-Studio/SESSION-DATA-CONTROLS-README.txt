STIMTAKE V1 - SESSION / SUPPORTER / SECRET SHOW FIX
===================================================

1) LAST SUPPORTER
- Last Supporter is persistent session data, not hard-coded overlay text.
- TOP TIPPERS / FANS now includes a LAST SUPPORTER DISPLAY editor.
- SET / EDIT changes the username, token amount and label.
- CLEAR removes the manual value AND clears the current session's recent supporter.

2) MY SECRET SHOW STARTUP
- My Secret Show starts INACTIVE on every application launch.
- The camera cover module is not enabled just by opening Studio or viewing its settings page.
- It becomes active only after the user deliberately presses LOCK / UNLOCK / CANCEL / TEST / RESET / TEASE VIEW or SAVE + APPLY SETTINGS.
- Camera remains visible until the user deliberately activates the feature.

3) TOP TIPPERS / FANS BACKUPS
- SAVE BACKUP stores a manual snapshot of tippers.tsv and viewer profile state.
- LOAD BACKUP restores that manual snapshot.
- LOAD LAST restores the data snapshot captured from the previous app session.
- Restore operations sync the OBS overlay after loading.

4) ACTION BUTTONS
- Existing true toggle behavior remains: OFF at startup, click once ON, click again OFF.
- No host action timer / 120-second maximum.
