# StimTake Studio V6 — Daily Run Instructions

Use this short routine for normal daily shows after first-time setup is complete.

## Daily Startup Order

```text
1. Open Google Chrome
2. Verify StimTakeChaturbateBridge is enabled
3. Open your Chaturbate model room
4. Open OBS Studio
5. Start StimTake-Studio-6.0.exe
6. Check My Room
7. Load your Show Pack
8. Check Action Deck tip amounts
9. Verify the OBS Browser Source
10. Start the show
```

## Chrome

Keep your Chaturbate model room open in the Chrome session containing the StimTake bridge.

The bridge must remain enabled while the show is running.

## OBS

Open OBS Studio and select your normal broadcast scene.

Make sure the StimTake Browser Source is visible and positioned correctly.

## StimTake Studio

Run:

```text
D:\StimTake-Studio\StimTake-Studio\outputs\v6\StimTake-Studio-6.0.exe
```

Check **My Room** and make sure it still matches your Chaturbate model URL.

## Load Your Show Pack

Open:

```text
Action Deck
→ Import Show Pack
```

Choose the **Daily Pack** or **Halloween Pack**.

Review the enabled actions and tip amounts.

## During the Show

Keep these running:

```text
Google Chrome
StimTakeChaturbateBridge
Your Chaturbate model room
OBS Studio
StimTake Studio 6.0
```

Normal live flow:

```text
Chaturbate Tip
      ↓
StimTakeChaturbateBridge
      ↓
StimTake Studio
      ↓
Action Deck
      ↓
OBS Overlay
```

## Quick Troubleshooting

If tips do not trigger:

```text
Check Chrome
Check the bridge
Check the correct model room
Check My Room
Check the Action Deck
```

If the action triggers but OBS shows nothing:

```text
Check the OBS Browser Source
Check the active OBS scene
Check that the action is enabled
```

## End of Show

```text
1. End the OBS broadcast
2. Close StimTake Studio
3. Close the Chaturbate model-room Chrome window
4. Close OBS when finished
```

That is the normal daily workflow.
