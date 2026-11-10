# Voice eval

Measures the command model against each screen's test set (`TestSetRow` in `src/voice/contracts.ts`): the right
action, the right arguments, the exact note text and the time taken. It runs on a workstation, not in CI.

## Fetch the models

TODO: how to fetch the pinned model files from `src/voice/manifest.ts`.

## Export the screen packs

TODO: how to export each screen's voice spec, grammar and test set.

## Run

```bash
cd src/ShipLean.Mobile
pnpm exec vitest run --config tools/voice-eval/vitest.config.ts
```

TODO: what a run reports and how to read it.
