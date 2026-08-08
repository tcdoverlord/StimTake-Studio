# StimTake Studio V1

> **Creator automation, live-event integration, OBS overlays,
> interactive games, token goals, supporter tracking, show actions,
> layouts, themes, and modular platform connectors --- built as one
> creator-controlled system.**

------------------------------------------------------------------------

## Current Build Status

**Milestone:** Live Platform Integration\
**Overall Status:** 🟡 In Progress\
**Estimated V1 Progress:** 75%\
**Current Goal:** Complete reliable Chaturbate tip detection using the
new StimTake Browser Bridge while preserving the Events API connector as
a second supported connection method.

``` text
STIMTAKE STUDIO V1

████████████░░░░  75%

CURRENT MILESTONE:
Chaturbate Live Tip Integration
```

------------------------------------------------------------------------

# Main Build Map

``` mermaid
flowchart TD
    CB[Chaturbate]
    API[Chaturbate Events API]
    BROWSER[StimTake Chrome Bridge]
    ADAPTER[StimTake Chaturbate Adapter]
    BUS[StimTake Event Bus]

    CB --> API
    CB --> BROWSER

    API --> ADAPTER
    BROWSER --> ADAPTER

    ADAPTER --> BUS

    BUS --> TIPPERS[Last Tipper + Top Tippers]
    BUS --> GOALS[Token Goals]
    BUS --> LOG[Tip Log]
    BUS --> GAMES[Dice + Wheel]
    BUS --> ACTIONS[Action Deck]
    BUS --> OVERLAYS[Overlay Engine]
    BUS --> OBS[OBS]
    BUS --> STATS[Statistics]
    BUS --> DASH[Dashboard]
    BUS --> LOVENSE[Lovense Adapter]
    BUS --> DB[Database]
    BUS --> STIMAPI[StimTake API]
```

The Events API work is **not being discarded**.

The new StimTake Chrome Bridge gives the Studio a second way to receive
authorized broadcaster-side tip activity.

Both methods ultimately produce the same kind of StimTake event.

------------------------------------------------------------------------

# Infrastructure Map

``` mermaid
flowchart LR
    DESKTOP[desktop-bjmnpla<br/>Windows Desktop<br/>OBS + Main Host]
    PI400[stimpi400<br/>Pi 400<br/>Linux Development]
    PI5[tcd-server<br/>Pi 5<br/>Server + API + Docker]
    TEST[Test Laptop<br/>Clean QA Machine]

    DESKTOP ---|Tailscale| PI400
    DESKTOP ---|Tailscale| PI5
    DESKTOP ---|Tailscale| TEST
    PI400 ---|SSH / Git / Deploy| PI5
```

------------------------------------------------------------------------

# Machine Roles

## Windows Desktop

**Role:** Main production and streaming machine

-   OBS Studio
-   Main StimTake control dashboard
-   Overlay testing
-   Local development tools
-   Final production control

## Pi 400 --- `stimpi400`

**Role:** Linux learning and development machine

-   Linux command-line practice
-   Python development
-   Git workflows
-   Adapter testing
-   Safe service experiments
-   Remote SSH access

**SSH command:**

``` powershell
ssh tcdoverlord@100.99.239.36
```

## Pi 5 --- `tcd-server`

**Role:** Main server

Planned services:

-   StimTake API
-   Docker services
-   Database
-   Event processing
-   WebSocket service
-   RTMP services
-   Monitoring

## Test Laptop

**Role:** Clean user testing environment

-   Install testing
-   First-run testing
-   Update testing
-   User experience testing
-   Proof-of-concept validation

------------------------------------------------------------------------

# Project Folder Layout

``` text
D:\StimTake-Studio
│
├── README.md
├── CHANGELOG.md
├── LICENSE
│
├── core
│   ├── event_bus
│   ├── rules
│   └── actions
│
├── adapters
│   ├── chaturbate
│   ├── obs
│   └── lovense
│
├── dashboard
├── overlay-engine
├── game-engine
├── api
├── database
├── docker
├── scripts
├── tests
├── docs
├── assets
├── builds
│
└── Third-Party
    └── chaturbate_poller
```

------------------------------------------------------------------------

# Chaturbate Integration

StimTake now follows a **dual-connector strategy**.

The platform-specific connector reports what happened. StimTake decides
what that event means for the show.

## Route A --- Chaturbate Events API

``` text
Chaturbate
     ↓
Events API
     ↓
StimTake Events Connector
     ↓
StimTake Studio
```

**Status:** 🟡 In development

The connector UI, LIVE/TESTBED selection, local event handling, and
supporting bridge work exist.

Reliable real-world incoming-tip delivery still requires end-to-end
validation.

## Route B --- StimTake Chrome Bridge

``` text
Chaturbate
     ↓
Broadcaster's Chrome Session
     ↓
StimTake Chrome Bridge
     ↓
localhost
     ↓
StimTake Studio
```

**Status:** 🟠 New development route

The first goal is intentionally small:

1.  Detect a received tip.
2.  Read the username.
3.  Read the token amount.
4.  Read the tip message.
5.  Send the event to StimTake locally.
6.  Process the event exactly once.

The Browser Bridge is intended to be a receiver.

It will not intentionally purchase tokens, send tips, automate
purchases, or access payment information.

------------------------------------------------------------------------

# Chaturbate Poller Reference

A local reference copy has been stored at:

``` text
D:\StimTake-Studio\Third-Party\chaturbate_poller
```

Source repository:

`https://github.com/MountainGod2/chaturbate_poller`

The third-party poller is **not StimTake Studio itself** and should be
treated as reference/experimental integration work rather than a
permanent requirement of the StimTake architecture.

The production goal is for StimTake-owned adapters to translate
platform-specific events into StimTake's internal event format.

Example:

``` json
{
  "source": "chaturbate",
  "event": "TIP_RECEIVED",
  "username": "example_user",
  "tokens": 50
}
```

That event can then trigger several actions:

``` mermaid
flowchart LR
    TIP[50 Token Tip]
    TIP --> SOUND[Play Sound]
    TIP --> OBS[Trigger OBS Action]
    TIP --> OVERLAY[Show Animation]
    TIP --> GOAL[Update Goal]
    TIP --> GAME[Run Game Action]
    TIP --> TOY[Trigger Device Action]
    TIP --> STATS[Save Statistics]
```

------------------------------------------------------------------------

# Core Design Rule

## The connector reports what happened.

``` text
Viewer123 tipped 25 tokens.
Message: !dice
```

## StimTake decides what that means.

``` text
Update Last Tipper
Update Top Tippers
Update Token Goal
Record Tip
Show Popup
Detect !dice
Run Dice if creator enabled automatic actions
Update OBS Overlay
```

This keeps StimTake modular and allows future platform adapters without
rewriting the entire system.

------------------------------------------------------------------------

# Planned Core Architecture

``` mermaid
flowchart TD
    INPUTS[Platform Inputs]
    INPUTS --> CBAPI[Chaturbate Events API Adapter]
    INPUTS --> CBBROWSER[StimTake Chrome Bridge Adapter]
    INPUTS --> FUTURE[Future Platform Adapters]

    CBAPI --> BUS[StimTake Event Bus]
    CBBROWSER --> BUS
    FUTURE --> BUS

    BUS --> RULES[Rules Engine]
    RULES --> ACTIONS[Action Engine]

    ACTIONS --> OBS[OBS]
    ACTIONS --> OVERLAYS[Overlays]
    ACTIONS --> GAMES[Games]
    ACTIONS --> GOALS[Goals]
    ACTIONS --> AUDIO[Audio]
    ACTIONS --> DEVICES[Device Integrations]
    ACTIONS --> DATABASE[Database]
    ACTIONS --> DASHBOARD[Dashboard]
```

------------------------------------------------------------------------

# Browser Bridge Security Boundary

The StimTake Chrome Bridge is intended to have a deliberately narrow
job.

## Allowed responsibilities

-   Detect supported received-tip activity in an authorized broadcaster
    session
-   Obtain the viewer username needed for the event
-   Obtain the received token amount
-   Obtain the tip message
-   Translate the event into StimTake's normalized format
-   Send the event to StimTake on the local computer

## Outside the Bridge's intended responsibilities

-   Purchasing tokens
-   Spending tokens
-   Automatically sending tips
-   Automating purchases
-   Accessing payment information
-   Copying third-party spending automation

The browser connector should remain separate from StimTake's show logic.

------------------------------------------------------------------------

# Local Event Route

The intended local event path is:

``` text
Chaturbate Connector
        ↓
Normalized StimTake Event
        ↓
127.0.0.1 / Local StimTake Service
        ↓
StimTake Studio
        ↓
Rules + Actions
        ↓
OBS / Overlay / Games / Goals / Supporter Tracking
```

The creator should not need to understand the internal API, JSON,
polling, or browser scripting to operate the finished product.

------------------------------------------------------------------------

# Current Build Progress

  Component                             Status
  ------------------------------------- ------------------------------
  StimTake Studio Windows UI            ✅ Working
  Backstage Dashboard                   ✅ Working
  OBS browser overlay                   ✅ Working
  Action Deck                           ✅ Working
  20-action package system              🟡 In development
  Layout customization                  🟡 In development
  Theme / skin system                   🟡 In development
  Seasonal upgrade-pack architecture    🟡 In development
  Last Tipper display                   ✅ Local test working
  Tip popup                             ✅ Local test working
  Top Tippers / Fan Board               🟡 Integration testing
  Token goals                           🟡 Integration testing
  Dice system                           ✅ Present
  Prize wheel                           ✅ Present
  Local simulated tip                   ✅ Working
  Local platform-event receiver         ✅ Present
  Chaturbate connector UI               ✅ Present
  Chaturbate LIVE / TESTBED selector    ✅ Present
  Chaturbate Events API tip reception   🟡 Not yet proven end-to-end
  StimTake Chrome Bridge                🟠 Starting
  Real Chaturbate → StimTake tip        ⏳ Must prove
  Real tip → OBS overlay                ⏳ Must prove end-to-end
  Duplicate-tip protection              ⏳ Needed
  Connector diagnostics                 ⏳ Needed
  Installer / creator setup             ⏳ Finalization needed
  Clean-machine QA                      ⏳ Needed

------------------------------------------------------------------------

# V1 Progress

``` text
CORE STUDIO              ███████████████░  Strong
OBS / OVERLAYS           ███████████████░  Strong
ACTIONS / GAMES          ██████████████░░  Strong
CUSTOMIZATION            █████████████░░░  In Progress
THEME / UPGRADE PACKS    ███████████░░░░░  In Progress
TIP EVENT HANDLING       ████████████░░░░  Local Path Working
CHATURBATE API           ████████░░░░░░░░  Integration Testing
CHROME BRIDGE            ███░░░░░░░░░░░░░  New Route
INSTALLER / QA           ███████░░░░░░░░░  Remaining

ESTIMATED V1 TOTAL       ████████████░░░░  ~75%
```

The 75% figure is an engineering progress estimate, not a measured
test-coverage percentage. It should advance only when the corresponding
capability is demonstrated working.

------------------------------------------------------------------------

# What Gets Us From 75% to 100%

## 75% → 80% --- Prove the Chrome Bridge

Build the first StimTake Chrome Bridge and demonstrate:

``` text
REAL TIP
   ↓
Chrome
   ↓
StimTake
```

## 80% → 85% --- Complete the Tip Path

Demonstrate:

``` text
REAL TIP
   ↓
StimTake
   ↓
Last Tipper
Top Tippers
Goal
Tip Log
   ↓
OBS
```

## 85% → 90% --- Reliability

Add and validate:

-   Duplicate-tip protection
-   Reconnect handling
-   Connection diagnostics
-   Browser-disconnected warning
-   Safe failure behavior
-   Session reset and recovery

## 90% → 95% --- Creator Experience

Finish:

-   Easy connector setup
-   Model/account configuration
-   Upgrade-pack import
-   Layouts
-   Themes
-   Actions
-   Sensible defaults
-   Configuration persistence

## 95% → 100% --- Release Validation

Validate:

-   Clean Windows installation
-   Clean Chrome installation
-   Multiple broadcaster-account testing
-   OBS testing
-   Restart testing
-   Recovery testing
-   Documentation
-   Installer/package
-   Final known-good Git checkpoint

------------------------------------------------------------------------

# Definition of V1 Complete

StimTake V1 reaches 100% when a creator can install it on a clean
Windows machine and reliably perform this workflow:

``` text
Install StimTake
       ↓
Connect Chaturbate
       ↓
Connect OBS overlay
       ↓
Go live
       ↓
Receive real tip
       ↓
StimTake detects it exactly once
       ↓
Last Tipper updates
Top Tippers updates
Goal updates
Tip Log updates
       ↓
Requested game/action can trigger
       ↓
OBS shows the result
       ↓
Restart without losing required configuration
```

Until that complete path has been demonstrated, V1 remains in
development.

------------------------------------------------------------------------

# Safe Change Rules

This project follows a production-style change process.

``` mermaid
flowchart LR
    VERIFY[Verify Working State]
    BACKUP[Create Backup Point]
    CHANGE[Make One Change]
    TEST[Test Everything]
    PASS{Working?}
    SAVE[Document + Commit]
    ROLLBACK[Roll Back]

    VERIFY --> BACKUP
    BACKUP --> CHANGE
    CHANGE --> TEST
    TEST --> PASS
    PASS -->|Yes| SAVE
    PASS -->|No| ROLLBACK
```

## Rules

1.  **If it works, do not change it without a reason.**
2.  **Identify the current installation before updating it.**
3.  **Create a recovery point before infrastructure changes.**
4.  **Make one change at a time.**
5.  **Test before moving forward.**
6.  **Document the exact working command.**
7.  **Never store passwords, API tokens, or private keys in Git.**

------------------------------------------------------------------------

# Security Rules

Do not commit:

``` text
.env
API tokens
Passwords
Tailscale auth keys
SSH private keys
Database passwords
Lovense credentials
Personal user data
```

Recommended `.gitignore` entries:

``` gitignore
.env
.env.*
!.env.example

*.key
*.pem
*.pfx
*.p12

secrets/
private/
logs/
__pycache__/
*.pyc

.venv/
venv/

node_modules/
dist/
build/
```

------------------------------------------------------------------------

# Next Safe Milestone

## Milestone --- First Real Browser-Bridge Tip

Before increasing the V1 percentage:

-   Preserve the current known-good StimTake build
-   Preserve the existing Events API connector
-   Create the StimTake Chrome Bridge as a separate module
-   Give the extension only the permissions it actually needs
-   Detect one real received tip
-   Extract username, amount, and message
-   Deliver the event to StimTake locally
-   Verify the event is processed exactly once
-   Verify Last Tipper updates
-   Verify Top Tippers updates
-   Verify the OBS overlay receives the result
-   Record the working configuration
-   Create a verified local Git checkpoint when the repository is
    available

------------------------------------------------------------------------

# Vision

StimTake Studio is intended to become a modular creator automation
platform that connects:

-   Platform events
-   OBS Studio
-   Browser overlays
-   Token goals
-   Interactive games
-   Action Decks
-   Layouts
-   Themes and upgrade packs
-   Audio
-   Analytics
-   Device integrations
-   Remote services
-   Creator dashboards

The long-term goal is one control system that turns incoming events into
coordinated creator experiences while allowing platform connectors to
evolve independently.

------------------------------------------------------------------------

## Project Status

``` text
ESTIMATED V1:       ~75%
CORE STUDIO:        STRONG
OBS / OVERLAYS:     STRONG
ACTIONS / GAMES:    STRONG
CUSTOMIZATION:      IN PROGRESS
CHATURBATE API:     INTEGRATION TESTING
CHROME BRIDGE:      NEW DEVELOPMENT ROUTE
INSTALLER / QA:     REMAINING
CURRENT PRIORITY:   PROVE ONE REAL TIP END-TO-END
```

------------------------------------------------------------------------

**Protect what works. Preserve the truth. Build in modules.**
