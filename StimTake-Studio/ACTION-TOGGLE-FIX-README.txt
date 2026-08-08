STIMTAKE V1 - ACTION TOGGLE FIX

WHAT CHANGED
- All 20 Action buttons start OFF/dark every time Studio starts.
- An assigned Action is NOT green just because HTML is loaded.
- Click an assigned Action number once: ON / green.
- Click that same Action number again: OFF / dark.
- Action slots have NO Studio timer.
- The old Show seconds / 120-second Action setting is removed from the Action Deck.
- Action configuration no longer publishes or stores an Action duration.
- STOP ALL remains as an emergency way to shut off every Action.

IMPORTANT
The EXE is intentionally not included in this source package because the previous
compiled EXE contains the old UI. On Windows, double-click:

    0-BUILD-AND-START-UPDATED-V1.cmd

That compiles CreatorStudioV3.cs + CreatorCamLauncher.cs with the embedded
CreatorCamPayload.zip and then starts the newly built EXE.
