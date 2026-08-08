STIMTAKE CHATURBATE BRIDGE v1
=============================

WHAT IT DOES
------------
This is deliberately small. It does NOT create another streaming UI.

Each model:
1. Runs her own StimTake Studio on her PC.
2. Uses her own Chaturbate username and Events API token.
3. Starts this bridge.
4. The bridge listens for Chaturbate TIP events.
5. It sends username + token amount + tip message into the existing StimTake UI.
6. It keeps a local JSONL tip log.

Normalized event sent to StimTake:

  {
    "type": "tip",
    "username": "viewer_name",
    "amount": 25,
    "message": "!dice",
    "request": "dice",
    "source": "chaturbate",
    "event_id": "...",
    "timestamp": "..."
  }

GAME REQUESTS
-------------
request-mode=detect (default)
  Detect !dice / !roll / roll dice / !wheel / !spin / spin wheel.
  Record and forward the request, but do not automatically start the game.

request-mode=trigger
  Explicit opt-in. A qualifying tip request also sends a StimTake dice or wheel
  event. Minimum token values can be set with --dice-min and --wheel-min.

TOKEN SAFETY
------------
The token is NOT written to the config or logs.
Preferred inputs:
  STIMTAKE_CB_TOKEN environment variable
  secure getpass prompt

Do not place a real token in screenshots, README files, source code, Git, or
support messages.

REQUIREMENTS
------------
- Windows with Python 3 available through `py -3`
- StimTake Studio running locally on port 8787
- Chaturbate Events API token for that broadcaster

LOCAL TEST
----------
Start StimTake, then run:
  TEST-STIMTAKE-TIP.cmd

That verifies the StimTake side without contacting Chaturbate.

REAL CONNECTION
---------------
Run:
  START-BRIDGE.cmd

The model enters her username and token. The token remains in memory only.

LOG LOCATION
------------
%LOCALAPPDATA%\StimTakeStudio\ChaturbateBridge\logs\YYYY-MM-DD-tips.jsonl

VALIDATION
----------
Python syntax validation and a mock StimTake endpoint test were performed in
the build environment. Live Chaturbate API access and Windows StimTake runtime
were NOT tested here.
