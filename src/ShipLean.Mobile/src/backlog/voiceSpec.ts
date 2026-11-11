import type { BacklogLevel } from '@shiplean/core/api/backlog';
import type { MemberDto } from '@shiplean/core/api/projects';
import type { BacklogRowDto } from '@shiplean/core/api/views';
import type { PreviewLine } from '../actions/types';
import { rankPlan, type RankRow, type RankTarget } from '../actions/rankPlan';
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
import { copyNoteSpan, matchKey, unmatched } from '../voice/resolve';
import type { BacklogAction, BacklogMove } from './actions';
import { TOP, describeAddItem } from './add/send';
import { describeAddNote, describeApprove } from './item/send';
import { describeMove } from './move/send';
import { answerFor, type AskTopic } from './voiceAnswers';
import { blockedLine, matchItem, plannedKeys } from './voiceMatch';

export interface BacklogVoiceContext {
  projectId: string;
  level: BacklogLevel;
  // The visible rows.
  rows: BacklogRowDto[];
  // The level's rows in rank order.
  order: BacklogRowDto[];
  filters: { needsMe: boolean; open: boolean; epicKey: string | null; featureKey: string | null };
  // The parents at the Stories level.
  features: { id: string; key: string; title: string }[];
  members: MemberDto[];
}

export const FILTERED_MOVE_LINE = 'Clear the filter to move items.';
export const ONE_MOVE_WARNING = 'One move at a time. Say the next one after this.';

const BOTTOM = { slot: 'last', beforeItemId: null } as const;

const item = { type: 'string', description: 'A key such as SHIP-906, or words from the title' };

export const BACKLOG_ACTIONS: VoiceActionSpec[] = [
  {
    name: 'move_relative',
    description: 'Move an item directly above or below another item.',
    args: {
      properties: {
        item,
        relation: { type: 'string', enum: ['above', 'below'] },
        other: item,
      },
      required: ['item', 'relation', 'other'],
    },
  },
  {
    name: 'move_to',
    description: 'Move an item to the top, the bottom, or a given rank (position rank, with rank).',
    args: {
      properties: {
        item,
        position: { type: 'string', enum: ['top', 'bottom', 'rank'] },
        rank: { type: 'integer' },
      },
      required: ['item', 'position'],
    },
  },
  {
    name: 'move_by',
    description: 'Move an item up or down a number of places (one when no count is said).',
    args: {
      properties: {
        item,
        direction: { type: 'string', enum: ['up', 'down'] },
        count: { type: 'integer' },
      },
      required: ['item', 'direction'],
    },
  },
  {
    name: 'add_item',
    description:
      'Create a Story, Bug or Feature, optionally under a parent key. The title is copied word for word. It goes at the top unless the bottom is said.',
    args: {
      properties: {
        type: { type: 'string', enum: ['story', 'bug', 'feature'] },
        parent: { type: 'string', description: 'The key of the Feature (or Epic) it goes under' },
        title: { type: 'string' },
        position: { type: 'string', enum: ['top', 'bottom'] },
      },
      required: ['type', 'title'],
    },
    noteArg: 'title',
  },
  {
    name: 'filter',
    description: 'Show only what needs the person, only open items, or one Epic or Feature by key.',
    args: {
      properties: {
        filter: { type: 'string', description: 'needsMe, open, or an Epic or Feature key' },
      },
      required: ['filter'],
    },
  },
  {
    name: 'switch_level',
    description: 'Switch the list to Epics, Features or Stories.',
    args: {
      properties: { level: { type: 'string', enum: ['epics', 'features', 'stories'] } },
      required: ['level'],
    },
  },
  {
    name: 'approve_decision',
    description: 'Confirm decisions, by number, on a named item.',
    args: {
      properties: { item, decisions: { type: 'array', items: { type: 'integer' } } },
      required: ['item', 'decisions'],
    },
  },
  {
    name: 'add_note',
    description: 'Leave a note on a named item, word for word.',
    args: { properties: { item, note: { type: 'string' } }, required: ['item', 'note'] },
    noteArg: 'note',
  },
  {
    name: 'open',
    description: 'Open an item.',
    args: { properties: { item }, required: ['item'] },
  },
  {
    name: 'ask',
    description:
      'Answer a question about the visible list: what is blocked, what needs the person, what is next.',
    args: {
      properties: { topic: { type: 'string', enum: ['whatsBlocked', 'whatNeedsMe', 'whatsNext'] } },
      required: ['topic'],
    },
  },
  {
    name: 'plan_sprint',
    description: 'Plan a sprint.',
    args: { properties: {} },
    webOnly: 'Sprint planning: the web Backlog’s Planning view',
    webPage: { page: 'planning' },
  },
  {
    name: 'bulk_edit',
    description: 'Edit many items at once.',
    args: { properties: {} },
    webOnly: 'Bulk edit: the web Backlog',
    // The web Backlog at the level the phone shows.
    webPage: { page: 'backlog' },
  },
];

interface Done {
  actions: BacklogAction[];
  lines: PreviewLine[];
  view: ViewIntent | null;
}

type Outcome = { ok: true; done: Done } | { ok: false; failure: VoiceFailure };

const view = (v: ViewIntent): Outcome => ({ ok: true, done: { actions: [], lines: [], view: v } });
const refuse = (action: string, reason: 'notAllowed' | 'invalidArgs' | 'none'): Outcome => ({
  ok: false,
  failure: { kind: 'noValidAction', detail: { action, reason } },
});

const str = (v: unknown): string => (typeof v === 'string' ? v : '');

// The project key is the part of an item key before its number.
const projectKeyOf = (key: string) => key.slice(0, key.lastIndexOf('-'));

export function backlogVoiceSpec(ctx: BacklogVoiceContext): ScreenVoiceSpec<BacklogAction> {
  const filtering = ctx.filters.epicKey !== null || ctx.filters.featureKey !== null;
  const rankRows: RankRow[] = ctx.order.flatMap((r) =>
    r.rank === null ? [] : [{ id: r.id, key: r.key, rank: r.rank }],
  );

  // The Epic keys a Features row names as its parent.
  const epicKeys = () => [
    ...new Set(
      ctx.rows.flatMap((r) =>
        r.parent !== null && r.parent.type.id === 'epic' ? [r.parent.key] : [],
      ),
    ),
  ];

  const parentOf = (key: string): { id: string; key: string } | VoiceFailure => {
    if (ctx.level === 'stories') {
      const m = matchKey(
        key,
        ctx.features.map((f) => f.key),
      );
      const found = m.kind === 'match' ? ctx.features.find((f) => f.key === m.value) : undefined;
      return found ?? unmatched(key, 'key', m);
    }
    const m = matchKey(key, epicKeys());
    const parent =
      m.kind === 'match'
        ? ctx.rows.find((r) => r.parent !== null && r.parent.key === m.value)?.parent
        : undefined;
    return parent ?? unmatched(key, 'key', m);
  };

  // A named item, or the failure for it: ambiguous with numbered candidates, or what resolve.ts classifies.
  const find = (spoken: string, call: ParsedCall): BacklogRowDto | VoiceFailure => {
    const m = matchItem(spoken, ctx.rows);
    if (m.kind === 'one') return m.row;
    if (m.kind === 'many')
      return {
        kind: 'ambiguous',
        detail: {
          candidates: m.rows.map((r) => ({
            key: r.key,
            title: r.title,
            rank: r.rank ?? undefined,
          })),
          call,
        },
      };
    return unmatched(spoken, 'key', m.key);
  };
  const isFailure = (v: unknown): v is VoiceFailure =>
    typeof v === 'object' && v !== null && 'kind' in v && 'detail' in v;

  function move(call: ParsedCall, target: RankTarget | VoiceFailure, row: BacklogRowDto): Outcome {
    if (isFailure(target)) return { ok: false, failure: target };
    const plan = rankPlan(rankRows, row.id, target);
    if (plan === null) return refuse(call.action, 'none');
    const planned = plannedKeys(rankRows, row.id, plan.toRank);
    const action: BacklogMove = {
      area: 'backlog',
      kind: 'move',
      projectId: ctx.projectId,
      itemId: row.id,
      key: row.key,
      revision: row.revision,
      level: ctx.level,
      fromRank: plan.fromRank,
      toRank: plan.toRank,
      placement: plan.placement,
      undoPlacement: plan.undoPlacement,
      shifted: plan.shifted,
      warning: blockedLine(row.key, row.blockedBy, planned),
    };
    return { ok: true, done: { actions: [action], lines: [describeMove(action)], view: null } };
  }

  function resolveOne(call: ParsedCall, transcript: Transcript): Outcome {
    const a = call.args;
    const named = (spoken: string) => find(spoken, call);
    const moving = ['move_relative', 'move_to', 'move_by'].includes(call.action);
    if (moving && filtering) return refuse(call.action, 'notAllowed');

    switch (call.action) {
      case 'move_relative': {
        const row = named(str(a.item));
        if (isFailure(row)) return { ok: false, failure: row };
        const other = named(str(a.other));
        if (isFailure(other)) return { ok: false, failure: other };
        if (other.id === row.id) return refuse(call.action, 'none');
        return move(
          call,
          { to: a.relation === 'below' ? 'below' : 'above', otherId: other.id },
          row,
        );
      }
      case 'move_to': {
        const row = named(str(a.item));
        if (isFailure(row)) return { ok: false, failure: row };
        if (a.position === 'top') return move(call, { to: 'top' }, row);
        if (a.position === 'bottom') return move(call, { to: 'bottom' }, row);
        const rank = a.rank;
        if (typeof rank !== 'number' || !Number.isInteger(rank) || rank < 1)
          return refuse(call.action, 'invalidArgs');
        return move(call, { to: 'rank', rank }, row);
      }
      case 'move_by': {
        const row = named(str(a.item));
        if (isFailure(row)) return { ok: false, failure: row };
        const by = a.count === undefined ? 1 : a.count;
        if (typeof by !== 'number' || !Number.isInteger(by) || by < 1)
          return refuse(call.action, 'invalidArgs');
        return move(call, { to: a.direction === 'down' ? 'down' : 'up', by }, row);
      }
      case 'add_item': {
        const title = call.noteSpan === undefined ? null : copyNoteSpan(transcript, call.noteSpan);
        if (title === null)
          return { ok: false, failure: { kind: 'unclear', detail: { heard: transcript.text } } };
        const type = a.type;
        const allowed =
          ctx.level === 'stories' ? ['story', 'bug'] : ctx.level === 'features' ? ['feature'] : [];
        if (type !== 'story' && type !== 'bug' && type !== 'feature')
          return refuse(call.action, 'invalidArgs');
        if (!allowed.includes(type)) return refuse(call.action, 'notAllowed');
        let parent: { id: string; key: string } | null = null;
        if (typeof a.parent === 'string' && a.parent !== '') {
          const found = parentOf(a.parent);
          if (isFailure(found)) return { ok: false, failure: found };
          parent = found;
        }
        const action: BacklogAction = {
          area: 'backlog',
          kind: 'addItem',
          itemId: null,
          key: null,
          projectId: ctx.projectId,
          type,
          title,
          parentId: parent === null ? null : parent.id,
          parentKey: parent === null ? null : parent.key,
          placement: a.position === 'bottom' ? BOTTOM : TOP,
        };
        return {
          ok: true,
          done: { actions: [action], lines: [describeAddItem(action)], view: null },
        };
      }
      case 'filter': {
        const spoken = str(a.filter);
        const squashed = spoken.toLowerCase().replace(/[\s_-]+/g, '');
        if (squashed === 'needsme')
          return view({ kind: 'screen', command: 'filter', args: { chip: 'needsMe' } });
        if (squashed === 'open')
          return view({ kind: 'screen', command: 'filter', args: { chip: 'open' } });
        if (ctx.level === 'epics') return refuse(call.action, 'notAllowed');
        const stories = ctx.level === 'stories';
        const keys = stories ? ctx.features.map((f) => f.key) : epicKeys();
        const m = matchKey(spoken, keys);
        if (m.kind !== 'match') return { ok: false, failure: unmatched(spoken, 'key', m) };
        return view({
          kind: 'screen',
          command: 'filter',
          args: { chip: stories ? 'feature' : 'epic', key: m.value },
        });
      }
      case 'switch_level':
        return view({ kind: 'screen', command: 'level', args: { level: a.level } });
      case 'approve_decision': {
        const row = named(str(a.item));
        if (isFailure(row)) return { ok: false, failure: row };
        const numbers = a.decisions;
        if (
          !Array.isArray(numbers) ||
          numbers.length === 0 ||
          !numbers.every((n) => typeof n === 'number' && Number.isInteger(n) && n >= 1)
        )
          return refuse(call.action, 'invalidArgs');
        const action: BacklogAction = {
          area: 'backlog',
          kind: 'approve',
          itemId: row.id,
          key: row.key,
          numbers: numbers as number[],
        };
        return {
          ok: true,
          done: { actions: [action], lines: [describeApprove(action)], view: null },
        };
      }
      case 'add_note': {
        const body = call.noteSpan === undefined ? null : copyNoteSpan(transcript, call.noteSpan);
        if (body === null)
          return { ok: false, failure: { kind: 'unclear', detail: { heard: transcript.text } } };
        const row = named(str(a.item));
        if (isFailure(row)) return { ok: false, failure: row };
        const action: BacklogAction = {
          area: 'backlog',
          kind: 'addNote',
          itemId: row.id,
          key: row.key,
          body,
        };
        return {
          ok: true,
          done: { actions: [action], lines: [describeAddNote(action)], view: null },
        };
      }
      case 'open': {
        const row = named(str(a.item));
        if (isFailure(row)) return { ok: false, failure: row };
        return view({ kind: 'navigate', link: links.item(projectKeyOf(row.key), row.key) });
      }
      case 'ask':
        return view({
          kind: 'answer',
          content: answerFor(a.topic as AskTopic, ctx.rows),
        });
      default:
        return refuse(call.action, 'none');
    }
  }

  return {
    screen: 'backlog',
    actions: BACKLOG_ACTIONS,
    context: () => ({
      // The keys the correction sheets read: the items on screen in display order, the states the screen can set
      // (none: the phone Backlog sets no state) and the focused item (none: the list has no focus).
      rows: ctx.rows.map((r) => ({ key: r.key, title: r.title })),
      states: [],
      key: null,
      level: ctx.level,
      filters: ctx.filters,
      items: ctx.rows.map((r) => ({
        key: r.key,
        title: r.title,
        rank: r.rank,
        state: r.state?.name ?? null,
        parent: r.parent?.key ?? null,
      })),
      people: ctx.members.map((m) => m.user.name),
    }),
    hotwords: () => [
      ...ctx.rows.map((r) => r.key),
      'epics',
      'features',
      'stories',
      ...ctx.members.map((m) => m.user.name),
      'top',
      'bottom',
    ],
    toActions(calls, transcript): ResolveResult<BacklogAction> {
      if (calls.length === 0)
        return { ok: false, failure: { kind: 'unclear', detail: { heard: transcript.text } } };
      const actions: BacklogAction[] = [];
      const lines: PreviewLine[] = [];
      const rejected: { call: ParsedCall; failure: VoiceFailure }[] = [];
      let intent: ViewIntent | null = null;
      let firstMove: number | null = null;
      let extraMove = false;
      for (const call of calls) {
        const isMove = ['move_relative', 'move_to', 'move_by'].includes(call.action);
        if (isMove && firstMove !== null) {
          extraMove = true;
          rejected.push({
            call,
            failure: {
              kind: 'noValidAction',
              detail: { action: call.action, reason: 'notAllowed' },
            },
          });
          continue;
        }
        const out = resolveOne(call, transcript);
        if (!out.ok) {
          rejected.push({ call, failure: out.failure });
          continue;
        }
        if (isMove) firstMove = lines.length;
        actions.push(...out.done.actions);
        lines.push(...out.done.lines);
        intent ??= out.done.view;
      }
      if (extraMove && firstMove !== null) {
        const line = lines[firstMove];
        lines[firstMove] = {
          ...line,
          warning:
            line.warning === undefined ? ONE_MOVE_WARNING : `${line.warning}\n${ONE_MOVE_WARNING}`,
        };
      }
      if (actions.length === 0 && intent === null)
        return { ok: false, failure: rejected[0].failure };
      return { ok: true, actions, lines, rejected, view: intent };
    },
  };
}
