\# StimTake Studio V1



> \*\*Creator automation, event orchestration, OBS control, overlays, games, goals, analytics, and device integrations — built as one modular system.\*\*



\---



\## Current Build Status



\*\*Milestone:\*\* Infrastructure Foundation  

\*\*Overall Status:\*\* 🟡 In Progress  

\*\*Current Goal:\*\* Keep all three machines connected through Tailscale and preserve a known-good working state.



```text

STIMTAKE STUDIO V1

█████░░░░░░░░░░░  Infrastructure Foundation

```



\---



\# Main Build Map



```mermaid

flowchart TD

&#x20;   CB\[Chaturbate Events API]

&#x20;   POLLER\[chaturbate-poller<br/>Python Event Connector]

&#x20;   ADAPTER\[StimTake Chaturbate Adapter]

&#x20;   BUS\[StimTake Event Bus]



&#x20;   CB --> POLLER

&#x20;   POLLER --> ADAPTER

&#x20;   ADAPTER --> BUS



&#x20;   BUS --> OBS\[OBS Controller]

&#x20;   BUS --> OVERLAYS\[Overlay Engine]

&#x20;   BUS --> GAMES\[Game Engine]

&#x20;   BUS --> GOALS\[Token Goals]

&#x20;   BUS --> STATS\[Statistics]

&#x20;   BUS --> DASH\[Dashboard]

&#x20;   BUS --> LOVENSE\[Lovense Adapter]

&#x20;   BUS --> DB\[Database]

&#x20;   BUS --> API\[StimTake API]

```



\---



\# Infrastructure Map



```mermaid

flowchart LR

&#x20;   DESKTOP\[desktop-bjmnpla<br/>Windows Desktop<br/>OBS + Main Host]

&#x20;   PI400\[stimpi400<br/>Pi 400<br/>Linux Development]

&#x20;   PI5\[tcd-server<br/>Pi 5<br/>Server + API + Docker]

&#x20;   TEST\[Test Laptop<br/>Clean QA Machine]



&#x20;   DESKTOP ---|Tailscale| PI400

&#x20;   DESKTOP ---|Tailscale| PI5

&#x20;   DESKTOP ---|Tailscale| TEST

&#x20;   PI400 ---|SSH / Git / Deploy| PI5

```



\---



\# Machine Roles



\## Windows Desktop



\*\*Role:\*\* Main production and streaming machine



\- OBS Studio

\- Main StimTake control dashboard

\- Overlay testing

\- Local development tools

\- Final production control



\## Pi 400 — `stimpi400`



\*\*Role:\*\* Linux learning and development machine



\- Linux command-line practice

\- Python development

\- Git workflows

\- Adapter testing

\- Safe service experiments

\- Remote SSH access



\*\*SSH command:\*\*



```powershell

ssh tcdoverlord@100.99.239.36

```



\## Pi 5 — `tcd-server`



\*\*Role:\*\* Main server



Planned services:



\- StimTake API

\- Docker services

\- Database

\- Event processing

\- WebSocket service

\- RTMP services

\- Monitoring



\## Test Laptop



\*\*Role:\*\* Clean user testing environment



\- Install testing

\- First-run testing

\- Update testing

\- User experience testing

\- Proof-of-concept validation



\---



\# Project Folder Layout



```text

D:\\StimTake-Studio

│

├── README.md

├── CHANGELOG.md

├── LICENSE

│

├── core

│   ├── event\_bus

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

&#x20;   └── chaturbate\_poller

```



\---



\# Chaturbate Poller



A local reference copy is stored at:



```text

D:\\StimTake-Studio\\Third-Party\\chaturbate\_poller

```



Source repository:



```text

https://github.com/MountainGod2/chaturbate\_poller

```



The poller is not StimTake Studio itself.



Its job is to receive Chaturbate Events API data such as:



\- Tips

\- Chat messages

\- Room status changes

\- User interactions



StimTake Studio will translate those platform-specific events into its own internal event format.



Example:



```json

{

&#x20; "source": "chaturbate",

&#x20; "event": "TIP\_RECEIVED",

&#x20; "username": "example\_user",

&#x20; "tokens": 50

}

```



That event can then trigger several actions:



```mermaid

flowchart LR

&#x20;   TIP\[50 Token Tip]

&#x20;   TIP --> SOUND\[Play Sound]

&#x20;   TIP --> OBS\[Trigger OBS Action]

&#x20;   TIP --> OVERLAY\[Show Animation]

&#x20;   TIP --> GOAL\[Update Goal]

&#x20;   TIP --> GAME\[Run Game Action]

&#x20;   TIP --> TOY\[Trigger Device Action]

&#x20;   TIP --> STATS\[Save Statistics]

```



\---



\# Core Design Rule



\## The connector reports what happened.



```text

A user tipped 50 tokens.

```



\## StimTake decides what that means.



```text

Play animation

Update token goal

Trigger OBS action

Run device response

Save statistics

Update leaderboard

```



This keeps StimTake modular and allows future platform adapters without rewriting the entire system.



\---



\# Planned Core Architecture



```mermaid

flowchart TD

&#x20;   INPUTS\[Platform Inputs]

&#x20;   INPUTS --> CB\[Chaturbate Adapter]

&#x20;   INPUTS --> FUTURE\[Future Platform Adapters]



&#x20;   CB --> BUS\[StimTake Event Bus]

&#x20;   FUTURE --> BUS



&#x20;   BUS --> RULES\[Rules Engine]

&#x20;   RULES --> ACTIONS\[Action Engine]



&#x20;   ACTIONS --> OBS\[OBS]

&#x20;   ACTIONS --> OVERLAYS\[Overlays]

&#x20;   ACTIONS --> GAMES\[Games]

&#x20;   ACTIONS --> GOALS\[Goals]

&#x20;   ACTIONS --> AUDIO\[Audio]

&#x20;   ACTIONS --> DEVICES\[Device Integrations]

&#x20;   ACTIONS --> DATABASE\[Database]

&#x20;   ACTIONS --> DASHBOARD\[Dashboard]

```



\---



\# Build Progress



| Component | Status |

|---|---|

| Main project folder | ✅ Complete |

| Third-party source folder | ✅ Complete |

| Chaturbate poller cloned | ✅ Complete |

| Windows desktop connected to Tailscale | ✅ Complete |

| Pi 5 connected to Tailscale | ✅ Complete |

| Pi 400 connected to Tailscale | 🟡 Verify after recovery |

| Remote SSH to Pi 400 | 🟡 Re-test |

| Hardware roles defined | ✅ Complete |

| Initial architecture defined | ✅ Complete |

| Git repository for main project | ⏳ Next |

| Safe backup point | ⏳ Next |

| Event bus | ⏳ Not started |

| Chaturbate adapter wrapper | ⏳ Not started |

| OBS controller | ⏳ Not started |

| Overlay engine integration | ⏳ Not started |

| Lovense adapter | ⏳ Not started |

| Dashboard | ⏳ Not started |

| Database | ⏳ Not started |

| Docker stack | ⏳ Not started |

| Installer | ⏳ Not started |



\---



\# Safe Change Rules



This project follows a production-style change process.



```mermaid

flowchart LR

&#x20;   VERIFY\[Verify Working State]

&#x20;   BACKUP\[Create Backup Point]

&#x20;   CHANGE\[Make One Change]

&#x20;   TEST\[Test Everything]

&#x20;   PASS{Working?}

&#x20;   SAVE\[Document + Commit]

&#x20;   ROLLBACK\[Roll Back]



&#x20;   VERIFY --> BACKUP

&#x20;   BACKUP --> CHANGE

&#x20;   CHANGE --> TEST

&#x20;   TEST --> PASS

&#x20;   PASS -->|Yes| SAVE

&#x20;   PASS -->|No| ROLLBACK

```



\## Rules



1\. \*\*If it works, do not change it without a reason.\*\*

2\. \*\*Identify the current installation before updating it.\*\*

3\. \*\*Create a recovery point before infrastructure changes.\*\*

4\. \*\*Make one change at a time.\*\*

5\. \*\*Test before moving forward.\*\*

6\. \*\*Document the exact working command.\*\*

7\. \*\*Never store passwords, API tokens, or private keys in Git.\*\*



\---



\# Security Rules



Do not commit:



```text

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



```gitignore

.env

.env.\*

!.env.example



\*.key

\*.pem

\*.pfx

\*.p12



secrets/

private/

logs/

\_\_pycache\_\_/

\*.pyc



.venv/

venv/



node\_modules/

dist/

build/

```



\---



\# Next Safe Milestone



\## Milestone 2 — Known-Good Backup



Before building more features:



\- Confirm all three Tailscale devices are online

\- Confirm SSH access to `stimpi400`

\- Record current versions without changing them

\- Save network and recovery notes

\- Create a backup point

\- Initialize the main Git repository

\- Commit this README as the first project checkpoint



Suggested first commit:



```text

docs: create StimTake Studio V1 engineering dashboard

```



\---



\# Vision



StimTake Studio is intended to become a modular creator automation platform that connects:



\- Platform events

\- OBS Studio

\- Browser overlays

\- Token goals

\- Interactive games

\- Audio

\- Analytics

\- Device integrations

\- Remote services

\- Creator dashboards



The long-term goal is one control system that turns incoming events into coordinated creator experiences.



\---



\## Project Status



```text

FOUNDATION:      IN PROGRESS

NETWORK:         MOSTLY READY

ARCHITECTURE:    DEFINED

CORE SOFTWARE:   NOT STARTED

FIRST PRIORITY:  STABILITY + BACKUP

```



