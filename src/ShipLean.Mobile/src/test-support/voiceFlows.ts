// A small work item screen and provider-like internals for the voice flow tests. Pure (no React Native), so vitest
// and jest specs can both use it.
import type { AppAction, PreviewLine } from '../actions/types';
import { UNDO } from '../actions/undo';
import type {
  ParsedCall,
  ResolveResult,
  ScreenVoiceSpec,
  Transcript,
  VoiceFailure,
} from '../voice/contracts';
import { keptActions, turnOf } from '../voice/machine';
import { copyNoteSpan } from '../voice/resolve';
import { fakeRunner, type FakeRunner } from './actions';
import { fakeInternals, type FakeInternals } from './voiceInternals';

export interface FlowContext {
  key: string;
  state: string;
  revision: number;
  decisions: string[];
}

export interface FlowAction extends AppAction {
  area: 'workItem';
  kind: 'approve' | 'setState' | 'addNote';
  revision?: number;
  number?: string;
  from?: string;
  to?: string;
  text?: string;
}

// approve(number), set_state(state) and add_note (noteArg text): lines[i] describes actions[i]. An unknown decision
// is rejected as noMatch.
export function flowSpec(context: () => FlowContext): ScreenVoiceSpec<FlowAction> {
  return {
    screen: 'workItem',
    actions: [
      { name: 'approve', description: 'Approve a decision', args: {} },
      { name: 'set_state', description: 'Set the state', args: {} },
      { name: 'add_note', description: 'Add a note', args: {}, noteArg: 'text' },
    ],
    context: () => ({ ...context() }),
    hotwords: () => [],
    toActions: (calls: ParsedCall[], t: Transcript): ResolveResult<FlowAction> => {
      const ctx = context();
      const base = { area: 'workItem' as const, itemId: 'item-1', key: ctx.key };
      const actions: FlowAction[] = [];
      const lines: PreviewLine[] = [];
      const rejected: { call: ParsedCall; failure: VoiceFailure }[] = [];
      for (const call of calls) {
        if (call.action === 'approve') {
          const n = String(call.args.number);
          if (!ctx.decisions.includes(n)) {
            rejected.push({
              call,
              failure: { kind: 'noMatch', detail: { spoken: n, role: 'decision', span: null } },
            });
            continue;
          }
          actions.push({ ...base, kind: 'approve', number: n });
          lines.push({ id: `approve-${n}`, icon: 'check', text: `Approve ${n}`, removable: true });
        } else if (call.action === 'set_state') {
          const to = String(call.args.state);
          actions.push({ ...base, kind: 'setState', revision: ctx.revision, from: ctx.state, to });
          lines.push({
            id: 'state',
            icon: 'flag',
            text: `State: ${ctx.state} → ${to}`,
            removable: true,
          });
        } else if (call.action === 'add_note' && call.noteSpan !== undefined) {
          const text = copyNoteSpan(t, call.noteSpan);
          if (text === null) continue;
          actions.push({ ...base, kind: 'addNote', text });
          lines.push({
            id: 'note',
            icon: 'note',
            text: `Note on ${ctx.key}`,
            detail: text,
            removable: true,
          });
        }
      }
      return { ok: true, actions, lines, rejected, view: null };
    },
  };
}

export interface FlowHarness {
  v: FakeInternals;
  runner: FakeRunner;
  // Every transcript text the model was asked to parse, in order.
  parsed: string[];
}

// Internals that behave as the provider's: resolve parses with `model`, keeps the result as last() and shows the
// preview or failure; resolveCalls resolves without the model and keeps its result as last(); dispatch sends the kept
// lines' actions through the runner with the undo window for the source.
export function flowHarness(
  context: FlowContext,
  model: (text: string) => ParsedCall[],
): FlowHarness {
  const spec = flowSpec(() => context);
  const runner = fakeRunner();
  const parsed: string[] = [];
  let last: { calls: ParsedCall[]; transcript: Transcript; result: ResolveResult } | null = null;
  let source: 'voice' | 'typed' = 'voice';
  const v: FakeInternals = fakeInternals({
    runner,
    spec: () => spec,
    last: () => last,
    resolve: async (t, opts) => {
      v.resolved.push({ transcript: t, source: opts.source });
      parsed.push(t.text);
      source = opts.source;
      const calls = model(t.text);
      const result = spec.toActions(calls, t);
      last = { calls, transcript: t, result };
      const turn = turnOf(spec, result, t.text, opts.source);
      if (turn.kind === 'preview') v.set(turn.state);
      else if (turn.kind === 'failure')
        v.set({ kind: 'failure', failure: turn.failure, heard: t.text, source: opts.source });
    },
    resolveCalls: (calls, t) => {
      const result = spec.toActions(calls, t);
      last = { calls, transcript: t, result };
      return result;
    },
    dispatch: (lineIds) => {
      v.dispatched.push(lineIds);
      if (last === null || !last.result.ok) throw new Error('There is no preview to apply.');
      return runner.dispatch(keptActions(last.result, lineIds), {
        undo: source === 'typed' ? UNDO.typed : UNDO.voice,
        origin: source,
        allOrNothing: true,
        via: 'voice',
      });
    },
  });
  return { v, runner, parsed };
}
