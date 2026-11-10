// What the Inbox understands when it is spoken to (N9). Pure: the model's parsed calls become the same InboxActions a
// tap makes, each with a preview line, or a VoiceFailure. Words are copied from the transcript, never written here.
import type { InboxAskDto, InboxDto, InboxItemDto } from '@shiplean/core/api/inbox';
import type { PreviewLine } from '../actions/types';
import type {
  AnswerSheetContent,
  ParsedCall,
  ResolveResult,
  ScreenVoiceSpec,
  Transcript,
  VoiceActionSpec,
  VoiceFailure,
} from '../voice/contracts';
import { copyNoteSpan, matchKey, matchPerson, unmatched } from '../voice/resolve';
import { STATUS_MAX } from '../workItem/voiceSpec';
import { labelsOf, type InboxAction } from './actions';
import { compressLabels } from './rowModel';

const key = { type: 'string', description: 'A work item key such as SHIP-907' };
const person = { type: 'string', description: 'A name of the person who asked' };
const label = { type: 'string', description: 'An ask label such as D4 or Q2' };
const note = { type: 'string' };

export const INBOX_ACTIONS: VoiceActionSpec[] = [
  {
    name: 'approve',
    description: 'Approve (confirm the recommended answer of) open decisions, by number or all.',
    args: {
      properties: {
        key,
        decisions: { type: 'array', items: { type: 'integer' } },
        all: { type: 'boolean', description: 'Every open decision on the item' },
      },
    },
  },
  {
    name: 'chooseDecision',
    description:
      'Choose an answer for one decision (D label) or a question with options (Q label): the option or alternative said.',
    args: {
      properties: { key, label, option: { type: 'string' } },
      required: ['label'],
    },
  },
  {
    name: 'answer',
    description: 'Answer an open question, word for word. Name the item or who asked.',
    args: {
      properties: { key, person, question: { type: 'integer' }, note },
      required: ['note'],
    },
    noteArg: 'note',
  },
  {
    name: 'discuss',
    description: 'Discuss an open decision with a note, word for word. The decision stays open.',
    args: {
      properties: { key, decision: { type: 'integer' }, note },
      required: ['decision', 'note'],
    },
    noteArg: 'note',
  },
  {
    name: 'reply',
    description: 'Reply to a mention, word for word. Name the item or who mentioned the person.',
    args: {
      properties: { key, person, mention: { type: 'integer' }, note },
      required: ['note'],
    },
    noteArg: 'note',
  },
  {
    name: 'seen',
    description: 'Mark a mention seen. Name the item or who mentioned the person.',
    args: { properties: { key, person, mention: { type: 'integer' } } },
  },
  {
    name: 'stillTrue',
    description: 'Say the Feature status line is still true.',
    args: { properties: { key }, required: ['key'] },
  },
  {
    name: 'status',
    description: 'Set a new status line on a Feature, word for word.',
    args: { properties: { key, note }, required: ['key', 'note'] },
    noteArg: 'note',
  },
  {
    name: 'whatNeedsMe',
    description: 'Say what is waiting in the Inbox.',
    args: { properties: {} },
  },
];

const isInt = (v: unknown): v is number => typeof v === 'number' && Number.isInteger(v);
const str = (v: unknown): string => (typeof v === 'string' ? v : '');
const norm = (s: string) => s.toLowerCase().replace(/[^\p{L}\p{N}]+/gu, '');

const plural = (n: number, one: string, many: string) => `${n} ${n === 1 ? one : many}`;

// "2 decisions · 1 question": what each item waits for.
function countsOf(item: InboxItemDto): string {
  const n = (kind: InboxAskDto['kind']) => item.asks.filter((a) => a.kind === kind).length;
  return [
    n('decision') > 0 ? plural(n('decision'), 'decision', 'decisions') : null,
    n('question') > 0 ? plural(n('question'), 'question', 'questions') : null,
    n('mention') > 0 ? plural(n('mention'), 'mention', 'mentions') : null,
    n('status') > 0 ? 'status to check' : null,
  ]
    .filter((c) => c !== null)
    .join(' · ');
}

// What the Inbox waits on, in its own order: the answer to "what needs me" and S4's first-result sheet.
export function waitingAnswer(inbox: InboxDto): AnswerSheetContent {
  const n = inbox.items.length;
  return {
    lead: n === 0 ? 'Nothing waits on you' : `${plural(n, 'item waits', 'items wait')} on you`,
    rows: inbox.items.map((i) => ({ key: i.key, title: i.title, counts: countsOf(i) })),
  };
}

const askerOf = (ask: InboxAskDto) => ask.from?.name ?? ask.agentName ?? ask.label;

const fail = (failure: VoiceFailure) => ({ ok: false as const, failure });
const refuse = (action: string, reason: 'invalidArgs' | 'none') =>
  fail({ kind: 'noValidAction', detail: { action, reason } });

type Target = { ok: true; item: InboxItemDto } | { ok: false; failure: VoiceFailure };

interface Done {
  action: InboxAction;
  line: PreviewLine;
}
type Outcome = { ok: true; done: Done[]; view: null } | { ok: false; failure: VoiceFailure };

const posted = (k: string) => `Posted on ${k}. The next agent to pick it up reads it.`;

export function inboxVoiceSpec(inbox: InboxDto): ScreenVoiceSpec<InboxAction> {
  const { items } = inbox;
  const projectKeys = items.map((i) => i.key);

  const base = (item: InboxItemDto) => ({
    area: 'inbox' as const,
    projectId: inbox.projectId,
    itemId: item.id,
    key: item.key,
  });

  const lineOf = (action: InboxAction, text: string, detail?: string, warning?: string) => ({
    id: `inbox:${action.itemId}:${action.kind}:${labelsOf(action).join(',')}`,
    icon: action.kind === 'newStatus' ? 'edit' : action.kind === 'seen' ? 'check' : 'note',
    text,
    ...(detail === undefined ? {} : { detail }),
    ...(warning === undefined ? {} : { warning }),
    removable: true,
  });

  const noteOf = (call: ParsedCall, transcript: Transcript): string | null =>
    call.noteSpan === undefined ? null : copyNoteSpan(transcript, call.noteSpan);
  const unclearNote = (transcript: Transcript) =>
    fail({ kind: 'unclear', detail: { heard: transcript.text } });

  // The item a call is about: by key; else by who asked; else the one item with a matching open ask.
  function locate(
    call: ParsedCall,
    fits: (item: InboxItemDto) => boolean,
    byPerson: ((item: InboxItemDto, personId: string) => boolean) | null,
  ): Target {
    const spoken = str(call.args.key);
    if (spoken !== '') {
      const m = matchKey(spoken, projectKeys);
      if (m.kind !== 'match') return { ok: false, failure: unmatched(spoken, 'key', m) };
      const item = items.find((i) => i.key === m.value)!;
      return fits(item) ? { ok: true, item } : refuse(call.action, 'none');
    }
    let candidates = items.filter(fits);
    const who = str(call.args.person);
    if (who !== '' && byPerson !== null) {
      const askers = new Map<string, { id: string; name: string }>();
      for (const i of candidates)
        for (const a of i.asks) if (a.from !== null) askers.set(a.from.id, a.from);
      const m = matchPerson(who, [...askers.values()]);
      if (m.kind !== 'match') return { ok: false, failure: unmatched(who, 'person', m) };
      candidates = candidates.filter((i) => byPerson(i, m.value.id));
    }
    if (candidates.length === 0) return refuse(call.action, 'none');
    if (candidates.length > 1)
      return fail({
        kind: 'ambiguous',
        detail: { candidates: candidates.map((i) => ({ key: i.key, title: i.title })), call },
      });
    return { ok: true, item: candidates[0] };
  }

  const askOf = (
    item: InboxItemDto,
    kind: InboxAskDto['kind'],
    pick?: (a: InboxAskDto) => boolean,
  ) => item.asks.filter((a) => a.kind === kind && (pick?.(a) ?? true));

  // One ask among several of a kind: the number said, or the only one there is.
  function oneAsk(call: ParsedCall, asks: InboxAskDto[]) {
    if (asks.length === 1) return { ok: true as const, ask: asks[0] };
    return fail({
      kind: 'ambiguous',
      detail: {
        candidates: asks.map((a) => ({ key: a.label, title: askerOf(a) })),
        call,
      },
    });
  }

  function resolveOne(call: ParsedCall, transcript: Transcript): Outcome {
    const a = call.args;
    const one = (
      action: InboxAction,
      text: string,
      detail?: string,
      warning?: string,
    ): Outcome => ({
      ok: true,
      done: [{ action, line: lineOf(action, text, detail, warning) }],
      view: null,
    });
    switch (call.action) {
      case 'approve': {
        const wanted = a.decisions;
        if (wanted !== undefined && (!Array.isArray(wanted) || !wanted.every(isInt)))
          return refuse(call.action, 'invalidArgs');
        const hasDecisions = (i: InboxItemDto) => askOf(i, 'decision').length > 0;
        const t = locate(
          call,
          wanted === undefined || a.all === true
            ? hasDecisions
            : (i) =>
                askOf(i, 'decision', (d) => (wanted as number[]).includes(d.decision!.number))
                  .length > 0,
          null,
        );
        if (!t.ok) return t;
        const open = askOf(t.item, 'decision');
        const numbers =
          a.all === true || wanted === undefined
            ? open.map((d) => d.decision!.number)
            : [...new Set(wanted as number[])].sort((x, y) => x - y);
        if (
          numbers.length === 0 ||
          !numbers.every((n) => open.some((d) => d.decision!.number === n))
        )
          return refuse(call.action, 'none');
        const action: InboxAction = { ...base(t.item), kind: 'approve', numbers };
        const labels = compressLabels(labelsOf(action));
        if (numbers.length === 1)
          return one(
            action,
            `Confirm ${labels} on ${t.item.key}`,
            `Recommended: ${open.find((d) => d.decision!.number === numbers[0])!.decision!.recommended}`,
          );
        return one(
          action,
          `Confirm ${labels} on ${t.item.key}`,
          numbers.length === open.length
            ? `All ${numbers.length} recommended answers`
            : `${numbers.length} recommended answers`,
          "you haven't opened them yet",
        );
      }
      case 'chooseDecision': {
        const spoken = str(a.label);
        const m = /^\s*([DQ])\s*(\d+)\s*$/i.exec(spoken);
        if (m === null) return refuse(call.action, 'invalidArgs');
        const kind = m[1].toUpperCase() === 'D' ? 'decision' : 'question';
        const number = Number(m[2]);
        const numberOf = (x: InboxAskDto) => (x.decision ?? x.question)!.number;
        const t = locate(
          call,
          (i) => askOf(i, kind, (x) => numberOf(x) === number).length > 0,
          null,
        );
        if (!t.ok) return t;
        const ask = askOf(t.item, kind, (x) => numberOf(x) === number)[0];
        const said = str(a.option);
        if (kind === 'decision') {
          const d = ask.decision!;
          const keyword = norm(said);
          let index: number | null | undefined;
          if (said === '' || keyword === 'recommended' || keyword === norm(d.recommended))
            index = null;
          else if (keyword === 'alternative' && d.alternatives.length === 1) index = 0;
          else {
            const at = d.alternatives.findIndex((x) => norm(x) === keyword);
            index = at === -1 ? undefined : at;
          }
          if (index === undefined) return fail(unmatched(said, 'option', { kind: 'none' }));
          const action: InboxAction = {
            ...base(t.item),
            kind: 'chooseDecision',
            number,
            alternativeIndex: index,
          };
          return index === null
            ? one(action, `Confirm ${ask.label} on ${t.item.key}`, `Recommended: ${d.recommended}`)
            : one(action, `Change ${ask.label} on ${t.item.key}`, `To “${d.alternatives[index]}”`);
        }
        const options = ask.question!.options;
        const at = options.findIndex((x) => norm(x) === norm(said));
        if (said === '' || at === -1) return fail(unmatched(said, 'option', { kind: 'none' }));
        const action: InboxAction = {
          ...base(t.item),
          kind: 'chooseOption',
          number,
          optionIndex: at,
          option: options[at],
        };
        return one(
          action,
          `Answer ${ask.label} on ${t.item.key}`,
          `“${options[at]}”. ${posted(t.item.key)}`,
        );
      }
      case 'answer': {
        const body = noteOf(call, transcript);
        if (body === null) return unclearNote(transcript);
        if (a.question !== undefined && !isInt(a.question))
          return refuse(call.action, 'invalidArgs');
        const open = (i: InboxItemDto) =>
          askOf(
            i,
            'question',
            (q) => a.question === undefined || q.question!.number === a.question,
          );
        const t = locate(
          call,
          (i) => open(i).length > 0,
          (i, id) => open(i).some((q) => q.from?.id === id),
        );
        if (!t.ok) return t;
        const r = oneAsk(call, open(t.item));
        if (!r.ok) return r;
        const action: InboxAction = {
          ...base(t.item),
          kind: 'answer',
          number: r.ask.question!.number,
          body,
        };
        return one(
          action,
          `Answer ${askerOf(r.ask)} on ${t.item.key}`,
          `“${body}”. ${posted(t.item.key)}`,
        );
      }
      case 'discuss': {
        const body = noteOf(call, transcript);
        if (body === null) return unclearNote(transcript);
        if (!isInt(a.decision)) return refuse(call.action, 'invalidArgs');
        const number = a.decision;
        const t = locate(
          call,
          (i) => askOf(i, 'decision', (d) => d.decision!.number === number).length > 0,
          null,
        );
        if (!t.ok) return t;
        const action: InboxAction = { ...base(t.item), kind: 'discuss', number, body };
        return one(
          action,
          `Discuss D${number} on ${t.item.key}`,
          `“${body}”. ${posted(t.item.key)} D${number} stays open.`,
        );
      }
      case 'reply':
      case 'seen': {
        const body = call.action === 'reply' ? noteOf(call, transcript) : '';
        if (body === null) return unclearNote(transcript);
        if (a.mention !== undefined && !isInt(a.mention)) return refuse(call.action, 'invalidArgs');
        const open = (i: InboxItemDto) =>
          askOf(i, 'mention', (m) => a.mention === undefined || m.label === `@${a.mention}`);
        const t = locate(
          call,
          (i) => open(i).length > 0,
          (i, id) => open(i).some((m) => m.from?.id === id),
        );
        if (!t.ok) return t;
        const r = oneAsk(call, open(t.item));
        if (!r.ok) return r;
        const mention = { mentionId: r.ask.mention!.mentionId, label: r.ask.label };
        if (call.action === 'seen')
          return one(
            { ...base(t.item), kind: 'seen', ...mention },
            `Mark ${mention.label} seen on ${t.item.key}`,
          );
        return one(
          { ...base(t.item), kind: 'mentionReply', ...mention, body },
          `Reply to ${askerOf(r.ask)} on ${t.item.key}`,
          `“${body}”. ${posted(t.item.key)}`,
        );
      }
      case 'stillTrue':
      case 'status': {
        const text = call.action === 'status' ? noteOf(call, transcript) : '';
        if (text === null) return unclearNote(transcript);
        const t = locate(call, (i) => askOf(i, 'status').length > 0, null);
        if (!t.ok) return t;
        if (call.action === 'stillTrue')
          return one(
            { ...base(t.item), kind: 'stillTrue', revision: t.item.revision },
            `Status still true on ${t.item.key}`,
            askOf(t.item, 'status')[0].status!.statusLine,
          );
        if (text.length > STATUS_MAX || /[\r\n]/.test(text))
          return refuse(call.action, 'invalidArgs');
        return one(
          {
            ...base(t.item),
            kind: 'newStatus',
            revision: t.item.revision,
            text,
            from: askOf(t.item, 'status')[0].status!.statusLine,
          },
          `New status on ${t.item.key}`,
          `“${text}”`,
        );
      }
      default:
        return refuse(call.action, 'none');
    }
  }

  const asked = items.map((i) => ({ item: i, asks: i.asks }));
  const options = (ask: InboxAskDto) => [
    ...(ask.question?.options ?? []),
    ...(ask.decision === null ? [] : [ask.decision.recommended, ...ask.decision.alternatives]),
  ];

  return {
    screen: 'inbox',
    actions: INBOX_ACTIONS,
    context: () => ({
      rows: items.map((i) => ({ key: i.key, title: i.title })),
      states: [],
      key: null,
      items: asked.map(({ item, asks }) => ({
        key: item.key,
        type: item.type,
        title: item.title,
        asks: asks.map((a) => ({
          kind: a.kind,
          label: a.label,
          from: askerOf(a),
          ...(a.decision === null
            ? {}
            : {
                question: a.decision.question,
                recommended: a.decision.recommended,
                alternatives: a.decision.alternatives,
              }),
          ...(a.question === null
            ? {}
            : { question: a.question.text, options: a.question.options }),
          ...(a.mention === null ? {} : { excerpt: a.mention.excerpt }),
          ...(a.status === null ? {} : { statusLine: a.status.statusLine }),
        })),
      })),
    }),
    hotwords: () => [
      ...new Set([
        ...items.map((i) => i.key),
        ...items.flatMap((i) => i.asks.map((a) => a.label)),
        ...items
          .flatMap((i) => i.asks.flatMap((a) => [a.from?.name, a.agentName]))
          .filter((n): n is string => typeof n === 'string'),
        ...items.flatMap((i) => i.asks.flatMap(options)),
      ]),
    ],
    toActions(calls, transcript): ResolveResult<InboxAction> {
      if (calls.length === 0) return fail({ kind: 'unclear', detail: { heard: transcript.text } });
      const actions: InboxAction[] = [];
      const lines: PreviewLine[] = [];
      const rejected: { call: ParsedCall; failure: VoiceFailure }[] = [];
      let view: Extract<ResolveResult, { ok: true }>['view'] = null;
      for (const call of calls) {
        if (call.action === 'whatNeedsMe') {
          view ??= { kind: 'answer', content: waitingAnswer(inbox) };
          continue;
        }
        const out = resolveOne(call, transcript);
        if (!out.ok) {
          rejected.push({ call, failure: out.failure });
          continue;
        }
        for (const d of out.done) {
          actions.push(d.action);
          lines.push(d.line);
        }
      }
      if (actions.length === 0 && view === null) return fail(rejected[0].failure);
      return { ok: true, actions, lines, rejected, view };
    },
  };
}
