import type { InboxItemDto } from '@shiplean/core/api/inbox';
import type { FeatureRowDto } from '@shiplean/core/api/featuresOverview';
import type { MemberDto, StateDto } from '@shiplean/core/api/projects';
import type { FeatureHealth } from '@shiplean/core/api/screens';
import type { PreviewLine } from '../actions/types';
import { rankPlan, type RankRow } from '../actions/rankPlan';
import { links } from '../links';
import type {
  ParsedCall,
  ResolveResult,
  ScreenVoiceSpec,
  Transcript,
  ViewIntent,
  VoiceActionSpec,
  VoiceFailure,
} from '../voice/contracts';
import { copyNoteSpan, matchKey, matchState, unmatched } from '../voice/resolve';
import type { FeaturesAction } from './actions';
import { openCount } from './details/openChildren';
import { featuresExecutor } from './executor';
import type { FeatureChip, FeatureViewArgs } from './list/filters';
import { answerFor, type FeatureAskTopic } from './voiceAnswers';

export interface FeaturesVoiceContext {
  projectId: string;
  // True on one Feature's board, where move_rank and filter_open are not offered.
  board: boolean;
  focus: FeatureRowDto | null;
  rows: FeatureRowDto[];
  // In rank order.
  order: FeatureRowDto[];
  states: StateDto[];
  // The open asks per Feature, from the Inbox.
  asks: InboxItemDto[];
  members: MemberDto[];
  projectKey: string;
  // A chip is on: voice rank moves are refused, as the web's isFiltering.
  filtering: boolean;
}

// The longest status line the server keeps.
export const STATUS_MAX = 500;

const feature = {
  type: 'string',
  description: 'A Feature key such as SHIP-906. Leave it out for the Feature that is open.',
};

const HEALTHS = ['onTrack', 'atRisk', 'blocked'] as const;

const TAB_ACTIONS: VoiceActionSpec[] = [
  {
    name: 'ask_status',
    description: 'Answer what is blocked, what is at risk, or how far the Features are.',
    args: {
      properties: {
        topic: { type: 'string', enum: ['blocked', 'atRisk', 'howFar'] },
        feature,
      },
      required: ['topic'],
    },
  },
  {
    name: 'ask_changed',
    description: 'Open the digest of what changed.',
    args: { properties: { since: { type: 'string' } } },
  },
  {
    name: 'set_state',
    description: 'Move a Feature to another state.',
    args: {
      properties: { feature, state: { type: 'string', description: 'A state name' } },
      required: ['state'],
    },
  },
  {
    name: 'add_note',
    description: 'Leave a note on a Feature, word for word.',
    args: { properties: { feature, note: { type: 'string' } }, required: ['note'] },
    noteArg: 'note',
  },
  {
    name: 'set_status',
    description: 'Set a Feature’s one-line status, word for word.',
    args: { properties: { feature, note: { type: 'string' } }, required: ['note'] },
    noteArg: 'note',
  },
  {
    name: 'set_health',
    description: 'Mark a Feature on track, at risk or blocked.',
    args: {
      properties: { feature, health: { type: 'string', enum: [...HEALTHS] } },
      required: ['health'],
    },
  },
  {
    name: 'answer',
    description: 'Answer a question waiting on the person, word for word.',
    args: {
      properties: {
        feature,
        question: { type: 'integer', description: 'The question number, such as 2 for Q2' },
        note: { type: 'string' },
      },
      required: ['note'],
    },
    noteArg: 'note',
  },
  {
    name: 'approve',
    description: 'Confirm decisions by number. A range such as D1 to D8 is listed in full.',
    args: {
      properties: { feature, decisions: { type: 'array', items: { type: 'integer' } } },
      required: ['decisions'],
    },
  },
  {
    name: 'move_rank',
    description: 'Move a Feature directly above or below another Feature.',
    args: {
      properties: {
        feature,
        relation: { type: 'string', enum: ['above', 'below'] },
        other: {
          type: 'string',
          description: 'The Feature key to move above or below, such as SHIP-906.',
        },
      },
      required: ['feature', 'relation', 'other'],
    },
  },
  {
    name: 'filter_open',
    description:
      'Show only what needs the person, what is at risk, what is blocked, or everything; or open a Feature’s board.',
    args: {
      properties: {
        filter: { type: 'string', enum: ['needsMe', 'atRisk', 'blocked', 'all'] },
        board: { type: 'boolean' },
        feature,
      },
    },
  },
  {
    name: 'move_tasks',
    description: 'Move tasks between columns.',
    args: { properties: {} },
    webOnly: 'Tasks move on the web task board',
  },
  {
    name: 'create_feature',
    description: 'Create a Feature.',
    args: { properties: {} },
    webOnly: 'New Features: the web Backlog, or say it on the Backlog tab',
    webPage: { page: 'backlog', level: 'features' },
  },
];

export const FEATURES_ACTIONS: VoiceActionSpec[] = TAB_ACTIONS;

// On one Feature's board neither a rank move nor a filter applies.
export const BOARD_ACTIONS: VoiceActionSpec[] = TAB_ACTIONS.filter(
  (a) => a.name !== 'move_rank' && a.name !== 'filter_open',
);

interface Done {
  actions: FeaturesAction[];
  lines: PreviewLine[];
  view: ViewIntent | null;
}

type Outcome = { ok: true; done: Done } | { ok: false; failure: VoiceFailure };

const refuse = (action: string, reason: 'notAllowed' | 'invalidArgs' | 'none'): Outcome => ({
  ok: false,
  failure: { kind: 'noValidAction', detail: { action, reason } },
});

const view = (v: ViewIntent): Outcome => ({ ok: true, done: { actions: [], lines: [], view: v } });

const act = (action: FeaturesAction): Outcome => ({
  ok: true,
  done: { actions: [action], lines: [featuresExecutor.describe(action)], view: null },
});

const isFailure = (v: unknown): v is VoiceFailure =>
  typeof v === 'object' && v !== null && 'kind' in v && 'detail' in v;

const str = (v: unknown): string => (typeof v === 'string' ? v : '');

const BLOCKED_WORD = /\bblocked\b/i;

const CHIPS: Record<string, FeatureChip> = {
  needsMe: 'needsYou',
  atRisk: 'atRisk',
  blocked: 'blocked',
  all: 'all',
};

export function featuresVoiceSpec(ctx: FeaturesVoiceContext): ScreenVoiceSpec<FeaturesAction> {
  const rankRows: RankRow[] = ctx.order.map((r) => ({ id: r.id, key: r.key, rank: r.rank }));

  // The named Feature, else the focused one; none focused asks which, among the Features on screen.
  const find = (spoken: unknown, call: ParsedCall): FeatureRowDto | VoiceFailure => {
    if (spoken === undefined || spoken === '') {
      if (ctx.focus !== null) return ctx.focus;
      return {
        kind: 'ambiguous',
        detail: {
          candidates: ctx.rows.map((r) => ({ key: r.key, title: r.title, rank: r.rank })),
          call,
        },
      };
    }
    const said = str(spoken);
    const m = matchKey(
      said,
      ctx.rows.map((r) => r.key),
    );
    const row = m.kind === 'match' ? ctx.rows.find((r) => r.key === m.value) : undefined;
    return row ?? unmatched(said, 'key', m);
  };

  // The open asks on this Feature itself (not its stories), the number each carries.
  const openAsks = (row: FeatureRowDto) =>
    ctx.asks.filter((i) => i.key === row.key).flatMap((i) => i.asks);
  const openDecisions = (row: FeatureRowDto) =>
    openAsks(row).flatMap((a) => (a.decision === null ? [] : [a.decision]));
  const openQuestions = (row: FeatureRowDto) =>
    openAsks(row).flatMap((a) => (a.question === null ? [] : [a.question]));

  const ambiguousAsk = (
    call: ParsedCall,
    open: { number: number; title: string; prefix: string }[],
  ): Outcome => ({
    ok: false,
    failure: {
      kind: 'ambiguous',
      detail: {
        candidates: open.map((o) => ({ key: `${o.prefix}${o.number}`, title: o.title })),
        call,
      },
    },
  });

  const healthChange = (row: FeatureRowDto, health: FeatureHealth): FeaturesAction => ({
    area: 'features',
    kind: 'setHealth',
    projectId: ctx.projectId,
    itemId: row.id,
    key: row.key,
    revision: row.revision,
    health,
    from: row.health,
  });

  const noteOf = (call: ParsedCall, transcript: Transcript): string | null =>
    call.noteSpan === undefined ? null : copyNoteSpan(transcript, call.noteSpan);

  const unclearOutcome = (transcript: Transcript): Outcome => ({
    ok: false,
    failure: { kind: 'unclear', detail: { heard: transcript.text } },
  });

  function resolveOne(call: ParsedCall, transcript: Transcript): Outcome {
    const a = call.args;
    switch (call.action) {
      case 'ask_status': {
        let only: FeatureRowDto | null = null;
        // A question on the tab is about every Feature on screen, even with a row expanded; on a Feature's board
        // (or with a key named) it is about that one Feature.
        if (a.feature !== undefined || (ctx.board && ctx.focus !== null)) {
          const row = find(a.feature, call);
          if (isFailure(row)) return { ok: false, failure: row };
          only = row;
        }
        return view({
          kind: 'answer',
          content: answerFor(a.topic as FeatureAskTopic, ctx.rows, only),
        });
      }
      case 'ask_changed':
        return view({ kind: 'navigate', link: links.digest(ctx.projectKey) });
      case 'set_state': {
        const row = find(a.feature, call);
        if (isFailure(row)) return { ok: false, failure: row };
        const spoken = str(a.state);
        const m = matchState(spoken, ctx.states);
        if (m.kind !== 'match') return { ok: false, failure: unmatched(spoken, 'state', m) };
        return act({
          area: 'features',
          kind: 'setState',
          projectId: ctx.projectId,
          itemId: row.id,
          key: row.key,
          revision: row.revision,
          stateId: m.value.id,
          stateName: m.value.name,
          fromStateId: row.state.id,
          fromStateName: row.state.name,
          closing: m.value.category === 'completed',
          openStories: openCount(row.rollups),
        });
      }
      case 'add_note': {
        const body = noteOf(call, transcript);
        if (body === null) return unclearOutcome(transcript);
        const row = find(a.feature, call);
        if (isFailure(row)) return { ok: false, failure: row };
        return act({ area: 'features', kind: 'addNote', itemId: row.id, key: row.key, body });
      }
      case 'set_status': {
        const text = noteOf(call, transcript);
        if (text === null) return unclearOutcome(transcript);
        if (text.length > STATUS_MAX) return refuse(call.action, 'invalidArgs');
        const row = find(a.feature, call);
        if (isFailure(row)) return { ok: false, failure: row };
        const status: FeaturesAction = {
          area: 'features',
          kind: 'setStatus',
          projectId: ctx.projectId,
          itemId: row.id,
          key: row.key,
          revision: row.revision,
          text,
          from: row.statusLine,
        };
        // P-X1 / D4: the word "blocked" proposes health Blocked, as its own line the person can remove.
        const proposeBlocked = BLOCKED_WORD.test(text) && row.health !== 'blocked';
        const actions = proposeBlocked ? [status, healthChange(row, 'blocked')] : [status];
        return {
          ok: true,
          done: {
            actions,
            lines: actions.map((x) => featuresExecutor.describe(x)),
            view: null,
          },
        };
      }
      case 'set_health': {
        const row = find(a.feature, call);
        if (isFailure(row)) return { ok: false, failure: row };
        const health = HEALTHS.find((h) => h === a.health);
        if (health === undefined) return refuse(call.action, 'invalidArgs');
        return act(healthChange(row, health));
      }
      case 'answer': {
        const body = noteOf(call, transcript);
        if (body === null) return unclearOutcome(transcript);
        const row = find(a.feature, call);
        if (isFailure(row)) return { ok: false, failure: row };
        const open = openQuestions(row);
        if (open.length === 0) return refuse(call.action, 'none');
        const choices = open.map((q) => ({ number: q.number, title: q.text, prefix: 'Q' }));
        let number: number;
        if (a.question === undefined) {
          if (open.length > 1) return ambiguousAsk(call, choices);
          number = open[0].number;
        } else {
          if (typeof a.question !== 'number') return refuse(call.action, 'invalidArgs');
          if (!open.some((q) => q.number === a.question)) return ambiguousAsk(call, choices);
          number = a.question;
        }
        return act({
          area: 'features',
          kind: 'answer',
          itemId: row.id,
          key: row.key,
          number,
          body,
        });
      }
      case 'approve': {
        const row = find(a.feature, call);
        if (isFailure(row)) return { ok: false, failure: row };
        const numbers = a.decisions;
        if (
          !Array.isArray(numbers) ||
          numbers.length === 0 ||
          !numbers.every((n) => typeof n === 'number' && Number.isInteger(n) && n >= 1)
        )
          return refuse(call.action, 'invalidArgs');
        const open = openDecisions(row);
        if (open.length === 0) return refuse(call.action, 'none');
        if (!(numbers as number[]).every((n) => open.some((d) => d.number === n)))
          return ambiguousAsk(
            call,
            open.map((d) => ({ number: d.number, title: d.question, prefix: 'D' })),
          );
        const sorted = [...new Set(numbers as number[])].sort((x, y) => x - y);
        const approve: FeaturesAction = {
          area: 'features',
          kind: 'approve',
          itemId: row.id,
          key: row.key,
          numbers: sorted,
        };
        // The executor's line has no detail for an approval; the voice preview says who reads it.
        const line = featuresExecutor.describe(approve);
        return {
          ok: true,
          done: {
            actions: [approve],
            lines: [
              { ...line, detail: `Posted on ${row.key}. The next agent to pick it up reads it.` },
            ],
            view: null,
          },
        };
      }
      case 'move_rank': {
        if (ctx.board) return refuse(call.action, 'none');
        if (ctx.filtering) return refuse(call.action, 'notAllowed');
        const row = find(a.feature, call);
        if (isFailure(row)) return { ok: false, failure: row };
        const other = find(a.other, call);
        if (isFailure(other)) return { ok: false, failure: other };
        if (other.id === row.id) return refuse(call.action, 'none');
        const plan = rankPlan(rankRows, row.id, {
          to: a.relation === 'below' ? 'below' : 'above',
          otherId: other.id,
        });
        if (plan === null) return refuse(call.action, 'none');
        return act({
          area: 'features',
          kind: 'moveRank',
          projectId: ctx.projectId,
          itemId: row.id,
          key: row.key,
          fromRank: plan.fromRank,
          toRank: plan.toRank,
          placement: plan.placement,
          undoPlacement: plan.undoPlacement,
          shifted: plan.shifted,
        });
      }
      case 'filter_open': {
        if (ctx.board) return refuse(call.action, 'none');
        if (a.board === true) {
          const row = find(a.feature, call);
          if (isFailure(row)) return { ok: false, failure: row };
          return view({ kind: 'navigate', link: links.featureBoard(ctx.projectKey, row.key) });
        }
        const chip = CHIPS[str(a.filter)];
        if (chip === undefined) return refuse(call.action, 'invalidArgs');
        const args: FeatureViewArgs = { chip };
        return view({ kind: 'screen', command: 'filter', args });
      }
      default:
        return refuse(call.action, 'none');
    }
  }

  const maxDecision = ctx.asks
    .flatMap((i) => i.asks)
    .reduce((max, a) => Math.max(max, a.decision?.number ?? 0), 0);

  return {
    screen: 'features',
    actions: ctx.board ? BOARD_ACTIONS : FEATURES_ACTIONS,
    context: () => ({
      projectKey: ctx.projectKey,
      board: ctx.board,
      filtering: ctx.filtering,
      key: ctx.focus?.key ?? null,
      rows: ctx.rows.map((r) => ({ key: r.key, title: r.title })),
      states: ctx.states.map((s) => s.name),
      features: ctx.rows.map((r) => ({
        key: r.key,
        title: r.title,
        rank: r.rank,
        state: r.state.name,
        health: r.health,
      })),
      people: ctx.members.map((m) => m.user.name),
      openDecisions: ctx.asks.flatMap((i) =>
        i.asks.flatMap((a) =>
          a.decision === null ? [] : [{ key: i.key, number: a.decision.number }],
        ),
      ),
      openQuestions: ctx.asks.flatMap((i) =>
        i.asks.flatMap((a) =>
          a.question === null ? [] : [{ key: i.key, number: a.question.number }],
        ),
      ),
    }),
    hotwords: () => [
      ...ctx.rows.map((r) => r.key),
      ...ctx.states.map((s) => s.name),
      'on track',
      'at risk',
      'blocked',
      ...ctx.members.map((m) => m.user.name),
      ...Array.from({ length: maxDecision }, (_, i) => `D${i + 1}`),
    ],
    toActions(calls, transcript): ResolveResult<FeaturesAction> {
      if (calls.length === 0)
        return { ok: false, failure: { kind: 'unclear', detail: { heard: transcript.text } } };
      const actions: FeaturesAction[] = [];
      const lines: PreviewLine[] = [];
      const rejected: { call: ParsedCall; failure: VoiceFailure }[] = [];
      let intent: ViewIntent | null = null;
      for (const call of calls) {
        const out = resolveOne(call, transcript);
        if (!out.ok) {
          rejected.push({ call, failure: out.failure });
          continue;
        }
        actions.push(...out.done.actions);
        lines.push(...out.done.lines);
        intent ??= out.done.view;
      }
      if (actions.length === 0 && intent === null)
        return { ok: false, failure: rejected[0].failure };
      return { ok: true, actions, lines, rejected, view: intent };
    },
  };
}
