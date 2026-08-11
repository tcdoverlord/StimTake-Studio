STIMTAKE STUDIO - CHROME BRIDGE CONNECTOR
=========================================

PURPOSE
-------
Adds a dedicated receiver-only Chrome Bridge section to the existing CONNECTORS tab.

HOW IT CONNECTS
---------------
StimTake Studio already owns this localhost endpoint while the Studio is running:

  http://127.0.0.1:8787/api/platform-event

The StimTake Chrome Bridge extension sends normalized received-tip events to that endpoint.
There is no Start/Stop handshake between Studio and the extension. Studio listens; the
extension pushes a real tip when it detects one.

EXPECTED CHROME BRIDGE EVENT
----------------------------
  {
    "type": "tip",
    "username": "viewer_name",
    "amount": 25,
    "message": "",
    "request": "",
    "source": "chaturbate-browser",
    "room": "model_room",
    "parser": "split-node",
    "event_id": "dom-...",
    "timestamp": "..."
  }

NEW CONNECTORS DISPLAY
----------------------
The dedicated Chrome Bridge panel shows:
- Bridge Status: WAITING FOR CHROME BRIDGE TIP / RECEIVING
- localhost Studio endpoint
- room
- Delivered to Studio count
- last received username and token amount

The panel only counts normalized tip events whose source is "chaturbate-browser".
Other local adapters remain separate.

PRESERVED
---------
- Existing localhost server on 127.0.0.1:8787
- Existing /api/platform-event route
- Existing Chaturbate Events API connector as a separate legacy option
- Existing overlays, supporter updates, games, actions, layouts, sessions, and other UI
- Receiver-only Chrome Bridge architecture

REMOVED FROM THE CONNECTORS TAB
-------------------------------
- The visible "TEST 25 TOKEN TIP" button from the legacy Chaturbate connector section.

NOT ADDED
---------
- Tip sending
- Token purchasing
- Payment logic
- Password/cookie capture
- Chaturbate API-token requirements for the Chrome Bridge
- Cloud dependencies

MANUAL TEST
-----------
1. Keep the currently working D:\StimTake-Studio folder untouched until this candidate builds.
2. Extract this candidate to a separate folder.
3. Build/start StimTake Studio using the project's existing build command.
4. Open CONNECTORS.
5. Confirm the new STIMTAKE CHROME BRIDGE section says WAITING FOR CHROME BRIDGE TIP.
6. Reload the working StimTake Chrome Bridge extension.
7. Open the intended Chaturbate room and confirm the extension popup says WATCHING.
8. Wait for one real visible tip.
9. Confirm Studio changes to RECEIVING and shows the same room, username, and token amount.
10. Confirm Delivered to Studio increases by exactly one for that real tip.

No live Chaturbate/Windows runtime test was performed in this build environment.
