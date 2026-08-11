# StimTake Show Pack Contract — V6 Jump-Off

Status: schema version 1, implemented for local V6 Studio/Designer builds.

A Show Pack is portable creative content. It is not a trusted Windows plug-in.

## Responsibility split

Designer owns:
- action identity and metadata
- overlay HTML/CSS/JS content
- images, sounds, animations and theme assets
- preview and structural package validation

Studio owns:
- saved/locked model identity
- live tip reception and event validation
- model token price for each action
- enabled/disabled show assignment
- supporter/session state
- OBS runtime behavior

## Draft ZIP structure

```text
pack.json
theme/
  theme.json
  assets/
actions/
  action-01/
    action.json
    overlay.html
    assets/
  ...
  action-20/
```

## Draft pack.json

```json
{
  "schema_version": 1,
  "product": "StimTake Show Pack",
  "name": "Halloween Night",
  "id": "halloween-night",
  "version": "1.0.0",
  "theme": "halloween",
  "max_actions": 20
}
```

## Draft action.json

```json
{
  "schema_version": 1,
  "slot": 1,
  "id": "halloween-night-action-01",
  "name": "Black Cat",
  "type": "overlay",
  "overlay": "overlay.html",
  "duration": 12,
  "default_enabled": false
}
```

Token prices are deliberately NOT required by the pack contract.
They belong to the model's Studio configuration.

## Implemented security boundary

Before runtime activation, Studio and Designer use the same strict validator. It rejects:
- path traversal
- unexpected executable/script file types outside the approved web-content model
- absolute paths
- files outside the package root
- oversized/bomb archives
- malformed manifests
- duplicate action slots or IDs
- more than 20 actions

It also enforces bounded ZIP/file counts, per-file and expanded-size limits, compression-ratio checks, exact action-folder/slot agreement, unique action IDs/slots, `overlay.html` references, allowed web-content extensions, and a required root `pack.json` plus `theme/theme.json`.

Validated action HTML runs in the preserved overlay module loader's sandboxed iframe (`allow-scripts` without same-origin privileges). Packs are copied only into managed Studio content folders and are never passed to PowerShell, cmd, or an arbitrary Windows process launcher.

Imported packs must not gain arbitrary Windows command execution, registry access,
administrator privileges, browser credentials, cookies, API tokens, or payment access.
