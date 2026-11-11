// What a screen's voice spec says about itself in context(): the failure sheets read it and take nothing that
// isn't there.
import { isBacklogLevel, type BacklogLevelParam } from '../../links';
import type { ScreenVoiceSpec } from '../contracts';
import type { ClosestRow } from './closest';

const isString = (v: unknown): v is string => typeof v === 'string';

function isRow(v: unknown): v is ClosestRow {
  return (
    typeof v === 'object' &&
    v !== null &&
    isString((v as ClosestRow).key) &&
    isString((v as ClosestRow).title)
  );
}

export interface SpecContext {
  // The screen's own items, for the closest match offline.
  rows: ClosestRow[];
  // The states of the focused item's type, for a state that doesn't exist.
  states: string[];
  // The item the screen is on, when it is on one.
  focusKey: string | null;
  // The backlog level the screen shows, when it shows one, for the web Backlog at the same level.
  level: BacklogLevelParam | null;
}

export function readSpecContext(spec: ScreenVoiceSpec | null): SpecContext {
  const context = spec?.context() ?? {};
  return {
    rows: Array.isArray(context.rows) ? context.rows.filter(isRow) : [],
    states: Array.isArray(context.states) ? context.states.filter(isString) : [],
    focusKey: isString(context.key) ? context.key : null,
    level: isBacklogLevel(context.level) ? context.level : null,
  };
}
