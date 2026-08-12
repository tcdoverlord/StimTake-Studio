# StimTake Studio V6 — First-Time Setup Instructions

Use this guide the first time you set up StimTake Studio 6.0.

## 1. Install the StimTake Chaturbate Bridge

The Chrome bridge is required for live Chaturbate tip detection.

Repository location:

```text
D:\StimTake-Studio\StimTake-Studio\Connectors\StimTakeChaturbateBridge
```

In Google Chrome:

1. Open `chrome://extensions/`
2. Turn on **Developer mode**.
3. Click **Load unpacked**.
4. Select:

```text
D:\StimTake-Studio\StimTake-Studio\Connectors\StimTakeChaturbateBridge
```

5. Confirm the bridge is enabled.

## 2. Open Your Chaturbate Model Room in Chrome

Open your public model room:

```text
https://chaturbate.com/YOUR_MODEL_NAME/
```

Keep this room open in the Chrome session where the StimTake bridge is installed.

Chrome is used to keep track of supported Chaturbate tip events. Your actual broadcast should still be handled by OBS Studio.

## 3. Open OBS Studio

Start OBS Studio and prepare your normal broadcast scene.

OBS is responsible for the camera, microphone, scene layout, Browser Source overlays, and broadcast output.

Make sure your StimTake Browser Source is present in the scene you plan to use.

## 4. Start StimTake Studio 6.0

Run:

```text
D:\StimTake-Studio\StimTake-Studio\outputs\v6\StimTake-Studio-6.0.exe
```

StimTake Studio starts its local runtime automatically.

Current local backend:

```text
127.0.0.1:8787
```

## 5. Set Your Chaturbate Room

Inside StimTake Studio, open **My Room**.

Change it to your own Chaturbate model URL.

Example:

```text
chaturbate.com/YOUR_MODEL_NAME
```

Click **Save**.

This room setting is used to help reject events from the wrong Chaturbate room.

## 6. Import a Show Pack

Open:

```text
Action Deck
→ Import Show Pack
```

Current pack location:

```text
D:\StimTake-Studio\StimTake-Studio\20-actions
```

Current pack choices include:

- Daily Pack
- Halloween Pack

Choose the pack you want to use.

## 7. Review Tip Amounts

After importing the Show Pack, review the actions inside the Action Deck.

You may keep the default tip amounts or change the tip amounts for your own show.

Make sure the actions you want to use are enabled.

## 8. Verify the Full Connection

```text
Chaturbate in Chrome
        ↓
StimTakeChaturbateBridge
        ↓
StimTake Studio 6.0
        ↓
Action Deck / Show Pack
        ↓
OBS Browser Source
        ↓
Visible overlay on stream
```

## First-Time Setup Checklist

- [ ] Google Chrome installed.
- [ ] StimTakeChaturbateBridge loaded in Chrome.
- [ ] Bridge enabled.
- [ ] Correct Chaturbate model room open.
- [ ] OBS Studio installed and working.
- [ ] StimTake Browser Source present in OBS.
- [ ] StimTake Studio 6.0 launches.
- [ ] My Room contains the correct model URL.
- [ ] Show Pack imports successfully.
- [ ] Action Deck pricing reviewed.
- [ ] Desired actions enabled.
- [ ] Overlay appears correctly in OBS.

Once these steps are complete, use the Daily Run Instructions for normal shows.
