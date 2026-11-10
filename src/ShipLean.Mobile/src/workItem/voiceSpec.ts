import type { ItemAsksDto } from '@shiplean/core/api/asks';
import type { MemberDto, StateDto } from '@shiplean/core/api/projects';
import type { FeatureHealth } from '@shiplean/core/api/screens';
import type { WorkItemDto } from '@shiplean/core/api/workItems';
import type { PreviewLine } from '../actions/types';
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
import { copyNoteSpan, matchKey, matchPerson, matchState, unmatched } from '../voice/resolve';
import type { WorkItemAction } from './actions';
import { workItemExecutor } from './executor';
import { answerFor, type AskTopic } from './voiceAnswers';

export interface WorkItemVoiceContext {
  item: WorkItemDto;
  asks: ItemAsksDto | null;
  states: StateDto[];
  members: MemberDto[];
}

export const STATUS_MAX = 500;

const state = { type: 'string', description: 'A state name such as Active' };

export const WORK_ITEM_ACTIONS: VoiceActionSpec[] = [
  {
    name: 'add_note',
    description: 'Leave a note on this item, word for word.',
    args: { properties: { note: { type: 'string' } }, required: ['note'] },
    noteArg: 'note',
  },
  {
    name: 'set_state',
    description: 'Move this item to a state of its type.',
    args: { properties: { state }, required: ['state'] },
  },
  {
    name: 'approve_decision',
    description: 'Approve open decisions, by number.',
    args: {
      properties: { decisions: { type: 'array', items: { type: 'integer' } } },
      required: ['decisions'],
    },
  },
  {
    name: 'discuss_decision',
    description: 'Discuss an open decision with a note, word for word.',
    args: {
      properties: { decision: { type: 'integer' }, note: { type: 'string' } },
      required: ['decision', 'note'],
    },
    noteArg: 'note',
  },
  {
    name: 'reply',
    description: 'Answer an open question, word for word. The question number is optional.',
    args: {
      properties: { question: { type: 'integer' }, note: { type: 'string' } },
      required: ['note'],
    },
    noteArg: 'note',
  },
  {
    name: 'confirm_status',
    description: 'Say the Feature status line is still true.',
    args: { properties: {} },
  },
  {
    name: 'set_status',
    description: 'Set the Feature status line, word for word.',
    args: { properties: { note: { type: 'string' } }, required: ['note'] },
    noteArg: 'note',
  },
  {
    name: 'set_health',
    description: 'Set the Feature health.',
    args: {
      properties: { health: { type: 'string', enum: ['onTrack', 'atRisk', 'blocked'] } },
      required: ['health'],
    },
  },
  {
    name: 'assign',
    description: 'Give this item an owner.',
    args: {
      properties: { person: { type: 'string', description: 'A name of someone in the project' } },
      required: ['person'],
    },
  },
  {
    name: 'ask',
    description:
      'Answer a question about this item: what is left, blocked, who owns it, what needs the person.',
    args: {
      properties: {
        topic: {
          type: 'string',
          enum: ['whatsLeft', 'whatsBlocked', 'whoOwns', 'whatNeedsMe'],
        },
      },
      required: ['topic'],
    },
  },
  {
    name: 'open',
    description: 'Open a child, the parent or a linked item by key.',
    args: { properties: { key: { type: 'string' } }, required: ['key'] },
  },
  {
    name: 'edit_fields',
    description: 'Edit title, description, criteria, points, sprint or links.',
    args: { properties: {} },
    webOnly: 'Title, description, criteria, points, sprint or links: on the web item page',
  },
  {
    name: 'move_tasks',
    description: 'Move tasks between states or people on the task board.',
    args: { properties: {} },
    webOnly: 'Moving tasks: the web task board',
  },
];

interface Done {
  actions: WorkItemAction[];
  view: ViewIntent | null;
}

type Outcome = { ok: true; done: Done } | { ok: false; failure: VoiceFailure };

const refuse = (action: string, reason: 'notAllowed' | 'invalidArgs' | 'none'): Outcome => ({
  ok: false,
  failure: { kind: 'noValidAction', detail: { action, reason } },
});

const str = (v: unknown): string => (typeof v === 'string' ? v : '');
const isInt = (v: unknown): v is number => typeof v === 'number' && Number.isInteger(v);

const projectKeyOf = (key: string) => key.slice(0, key.lastIndexOf('-'));

export function workItemVoiceSpec(ctx: WorkItemVoiceContext): ScreenVoiceSpec<WorkItemAction> {
  const { item } = ctx;
  const isFeature = item.type.id === 'feature';
  const typeStates = ctx.states.filter((s) => s.type === item.type.id);
  const people = ctx.members
    .filter((m) => m.status === 'active')
    .map((m) => ({ id: m.user.id, name: m.user.name }));
  const decisions = ctx.asks?.decisions ?? [];
  const questions = ctx.asks?.questions ?? [];
  const openDecisions = decisions.filter((d) => d.state === 'open');
  const openQuestions = questions.filter((q) => q.state === 'open');

  // The items on screen: the parent, the children and what this item links to.
  const onScreen: { key: string; title: string }[] = [
    ...(item.parent === null ? [] : [{ key: item.parent.key, title: item.parent.title }]),
    ...item.children.map((c) => ({ key: c.key, title: c.title })),
    ...item.links.flatMap((l) => (l.key === null ? [] : [{ key: l.key, title: l.title ?? '' }])),
  ];

  const base = {
    area: 'workItem' as const,
    projectId: item.projectId,
    itemId: item.id,
    key: item.key,
  };

  const noteOf = (call: ParsedCall, transcript: Transcript): string | VoiceFailure => {
    const text = call.noteSpan === undefined ? null : copyNoteSpan(transcript, call.noteSpan);
    return text ?? { kind: 'unclear', detail: { heard: transcript.text } };
  };
  const isFailure = (v: unknown): v is VoiceFailure => typeof v === 'object' && v !== null;

  const ambiguous = (call: ParsedCall, open: { label: string; text: string }[]): Outcome => ({
    ok: false,
    failure: {
      kind: 'ambiguous',
      detail: { candidates: open.map((o) => ({ key: o.label, title: o.text })), call },
    },
  });
  const openDecisionList = openDecisions.map((d) => ({ label: d.label, text: d.question }));
  const openQuestionList = openQuestions.map((q) => ({ label: q.label, text: q.text }));

  function resolveOne(call: ParsedCall, transcript: Transcript): Outcome {
    const a = call.args;
    const one = (action: WorkItemAction): Outcome => ({
      ok: true,
      done: { actions: [action], view: null },
    });
    switch (call.action) {
      case 'add_note': {
        const body = noteOf(call, transcript);
        if (isFailure(body)) return { ok: false, failure: body };
        return one({ ...base, kind: 'addNote', body });
      }
      case 'set_state': {
        const spoken = str(a.state);
        const m = matchState(spoken, typeStates);
        if (m.kind !== 'match') return { ok: false, failure: unmatched(spoken, 'state', m) };
        return one({
          ...base,
          kind: 'setState',
          revision: item.revision,
          stateId: m.value.id,
          stateName: m.value.name,
          fromStateId: item.state.id,
          fromStateName: item.state.name,
        });
      }
      case 'approve_decision': {
        const numbers = a.decisions;
        if (!Array.isArray(numbers) || numbers.length === 0 || !numbers.every(isInt))
          return refuse(call.action, 'invalidArgs');
        const open = new Set(openDecisions.map((d) => d.number));
        if (!numbers.every((n) => open.has(n))) return ambiguous(call, openDecisionList);
        return one({ ...base, kind: 'approve', numbers: [...new Set(numbers)] });
      }
      case 'discuss_decision': {
        const body = noteOf(call, transcript);
        if (isFailure(body)) return { ok: false, failure: body };
        if (!isInt(a.decision)) return refuse(call.action, 'invalidArgs');
        const number = a.decision;
        if (!openDecisions.some((d) => d.number === number))
          return ambiguous(call, openDecisionList);
        return one({ ...base, kind: 'discuss', number, body });
      }
      case 'reply': {
        const body = noteOf(call, transcript);
        if (isFailure(body)) return { ok: false, failure: body };
        let number: number;
        if (a.question === undefined) {
          if (openQuestions.length !== 1) return ambiguous(call, openQuestionList);
          number = openQuestions[0].number;
        } else {
          if (!isInt(a.question)) return refuse(call.action, 'invalidArgs');
          number = a.question;
          if (!openQuestions.some((q) => q.number === number))
            return ambiguous(call, openQuestionList);
        }
        return one({ ...base, kind: 'reply', number, body });
      }
      case 'confirm_status':
        if (!isFeature) return refuse(call.action, 'notAllowed');
        return one({ ...base, kind: 'confirmStatus', revision: item.revision });
      case 'set_status': {
        if (!isFeature) return refuse(call.action, 'notAllowed');
        const text = noteOf(call, transcript);
        if (isFailure(text)) return { ok: false, failure: text };
        if (text.length > STATUS_MAX || /[\r\n]/.test(text))
          return refuse(call.action, 'invalidArgs');
        const status: WorkItemAction = {
          ...base,
          kind: 'setStatus',
          revision: item.revision,
          text,
          from: item.statusLine === '' ? null : item.statusLine,
        };
        // P-X1: a status that says blocked also proposes the health, as its own removable line.
        if (/\bblocked\b/i.test(text) && item.health !== 'blocked')
          return {
            ok: true,
            done: {
              actions: [
                status,
                {
                  ...base,
                  kind: 'setHealth',
                  revision: item.revision,
                  health: 'blocked',
                  from: item.health,
                },
              ],
              view: null,
            },
          };
        return one(status);
      }
      case 'set_health': {
        if (!isFeature) return refuse(call.action, 'notAllowed');
        const health = a.health;
        if (health !== 'onTrack' && health !== 'atRisk' && health !== 'blocked')
          return refuse(call.action, 'invalidArgs');
        return one({
          ...base,
          kind: 'setHealth',
          revision: item.revision,
          health: health satisfies FeatureHealth,
          from: item.health,
        });
      }
      case 'assign': {
        const spoken = str(a.person);
        const m = matchPerson(spoken, people);
        if (m.kind !== 'match') return { ok: false, failure: unmatched(spoken, 'person', m) };
        return one({
          ...base,
          kind: 'assign',
          revision: item.revision,
          userId: m.value.id,
          name: m.value.name,
          fromUserId: item.assignee?.id ?? null,
          fromName: item.assignee?.name ?? null,
        });
      }
      case 'ask': {
        const topic = a.topic;
        if (
          topic !== 'whatsLeft' &&
          topic !== 'whatsBlocked' &&
          topic !== 'whoOwns' &&
          topic !== 'whatNeedsMe'
        )
          return refuse(call.action, 'invalidArgs');
        return {
          ok: true,
          done: {
            actions: [],
            view: { kind: 'answer', content: answerFor(topic satisfies AskTopic, item, ctx.asks) },
          },
        };
      }
      case 'open': {
        const spoken = str(a.key);
        const m = matchKey(
          spoken,
          onScreen.map((r) => r.key),
        );
        if (m.kind !== 'match') return { ok: false, failure: unmatched(spoken, 'key', m) };
        return {
          ok: true,
          done: {
            actions: [],
            view: { kind: 'navigate', link: links.item(projectKeyOf(item.key), m.value) },
          },
        };
      }
      default:
        return refuse(call.action, 'none');
    }
  }

  return {
    screen: 'workItem',
    actions: WORK_ITEM_ACTIONS,
    context: () => ({
      // The keys the correction sheets read: the items on screen in display order, the states this item can be
      // set to and the focused item.
      rows: onScreen,
      states: typeStates.map((s) => s.name),
      key: item.key,
      item: {
        key: item.key,
        title: item.title,
        type: item.type.id,
        state: item.state.name,
        owner: item.assignee?.name ?? null,
        health: item.health,
      },
      decisions: openDecisions.map((d) => ({ number: d.number, question: d.question })),
      questions: openQuestions.map((q) => ({ number: q.number, text: q.text })),
      people: people.map((p) => p.name),
    }),
    hotwords: () => [
      item.key,
      ...onScreen.map((r) => r.key),
      ...typeStates.map((s) => s.name),
      ...people.map((p) => p.name),
      ...decisions.map((d) => d.label),
      ...questions.map((q) => q.label),
    ],
    toActions(calls, transcript): ResolveResult<WorkItemAction> {
      if (calls.length === 0)
        return { ok: false, failure: { kind: 'unclear', detail: { heard: transcript.text } } };
      const actions: WorkItemAction[] = [];
      const lines: PreviewLine[] = [];
      const rejected: { call: ParsedCall; failure: VoiceFailure }[] = [];
      let view: ViewIntent | null = null;
      for (const call of calls) {
        const out = resolveOne(call, transcript);
        if (!out.ok) {
          rejected.push({ call, failure: out.failure });
          continue;
        }
        for (const action of out.done.actions) {
          actions.push(action);
          const line = workItemExecutor.describe(action);
          // A discussion post and a reply are read by the next agent: the preview quotes the words and says where they go.
          lines.push(
            action.kind === 'discuss' || action.kind === 'reply'
              ? {
                  ...line,
                  text: `${line.text}: "${action.body}"`,
                  detail: `Posted on ${action.key}. The next agent to pick it up reads it.`,
                }
              : line,
          );
        }
        view ??= out.done.view;
      }
      if (actions.length === 0 && view === null) return { ok: false, failure: rejected[0].failure };
      return { ok: true, actions, lines, rejected, view };
    },
  };
}
