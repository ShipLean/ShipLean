import {
  RETRO_CATEGORIES,
  type RetroCategory,
  type RetroMergeProposalDto,
  type RetroPointDto,
} from '@shiplean/core/api/retros';
import type { MemberDto } from '@shiplean/core/api/projects';
import type { PreviewLine } from '../actions/types';
import type {
  ParsedCall,
  ResolveResult,
  ScreenVoiceSpec,
  Transcript,
  ViewIntent,
  VoiceActionSpec,
  VoiceFailure,
} from '../voice/contracts';
import { copyNoteSpan, matchKey, matchPerson, unmatched } from '../voice/resolve';
import type { RetroAction } from './actions';
import { aboutOf } from './add/about';
import { seedTitle } from './decide/actionTitle';
import { retroExecutor } from './executor';
import { CATEGORY_LABELS, type RetroView, type RetroViewArgs } from './notes/notesModel';
import { retroAnswer, type RetroAskTopic } from './voiceAnswers';
import { isThat, matchPoint } from './voiceMatch';

export type RetroVoiceContext = {
  sprint: { id: string; name: string } | null;
  category: RetroCategory | null;
  points: RetroPointDto[];
  lastTouchedId: string | null;
  openActions: { workItemId: string; key: string; title: string }[];
  proposals: RetroMergeProposalDto[];
  members: MemberDto[];
  projectId: string;
  projectKey: string;
};

const category = { type: 'string', enum: [...RETRO_CATEGORIES] };
const point = { type: 'string', description: '"that" or words from the point' };

export const RETRO_ACTIONS: VoiceActionSpec[] = [
  {
    name: 'add_point',
    description: 'Add a retro point, word for word. Pick the category if none was said.',
    args: { properties: { note: { type: 'string' }, category }, required: ['note', 'category'] },
    noteArg: 'note',
  },
  {
    name: 'support',
    description: 'Plus one a point.',
    args: { properties: { point }, required: ['point'] },
  },
  {
    name: 'make_action',
    description: 'Make an action item from a point, with an owner if one was named.',
    args: {
      properties: {
        point,
        person: { type: 'string', description: 'A name of someone in the project' },
      },
      required: ['point'],
    },
  },
  {
    name: 'add_to_action',
    description: 'Add a point to an open action item, by key.',
    args: {
      properties: { point, action: { type: 'string', description: 'An action item key' } },
      required: ['point', 'action'],
    },
  },
  {
    name: 'not_needed',
    description: 'Mark a point as not needed, with the reason if one was given.',
    args: {
      properties: { point, reason: { type: 'string' } },
      required: ['point'],
    },
    // Not a noteArg: the grammar makes a noteSpan mandatory for those, and a reason may be left out (P-R4).
  },
  {
    name: 'answer_merge',
    description: 'Merge two themes or keep them apart.',
    args: {
      properties: {
        answer: { type: 'string', enum: ['merge', 'keepApart'] },
        proposal: { type: 'string', description: 'A theme name in the proposal' },
      },
      required: ['answer'],
    },
  },
  {
    name: 'show',
    description: 'Show a category, the points to decide or the Features.',
    args: {
      properties: { view: { type: 'string', enum: [...RETRO_CATEGORIES, 'toDecide', 'features'] } },
      required: ['view'],
    },
  },
  {
    name: 'ask',
    description:
      'Answer a question about the retro: what keeps going wrong, what is left, what went well.',
    args: {
      properties: {
        topic: { type: 'string', enum: ['keepsGoingWrong', 'leftToDecide', 'whatWentWell'] },
      },
      required: ['topic'],
    },
  },
  {
    name: 'add_point_in',
    description: 'Add a retro point in the category that was said, word for word.',
    args: { properties: { category, note: { type: 'string' } }, required: ['category', 'note'] },
    noteArg: 'note',
  },
  {
    name: 'rename_theme',
    description: 'Rename or merge themes by hand.',
    args: { properties: {} },
    webOnly: 'Renaming and merging themes by hand: the web Insights',
  },
  {
    name: 'archive_point',
    description: 'Archive a point.',
    args: { properties: {} },
    webOnly: 'Archiving a point: the web retro board',
  },
];

type Outcome =
  | { ok: true; actions: RetroAction[]; view: ViewIntent | null }
  | { ok: false; failure: VoiceFailure };

const str = (v: unknown): string => (typeof v === 'string' ? v : '');
const isCategory = (v: unknown): v is RetroCategory => RETRO_CATEGORIES.some((c) => c === v);

const refuse = (action: string, reason: 'notAllowed' | 'invalidArgs' | 'none'): Outcome => ({
  ok: false,
  failure: { kind: 'noValidAction', detail: { action, reason } },
});

export function retroVoiceSpec(ctx: RetroVoiceContext): ScreenVoiceSpec<RetroAction> {
  const people = ctx.members.map((m) => m.user);
  const touched = ctx.points.find((p) => p.id === ctx.lastTouchedId) ?? null;
  const candidates = ctx.points
    .filter((p) => p.decision === 'undecided')
    .map((p) => ({ key: p.workItem?.key ?? '', title: p.text }));

  const ask = (call: ParsedCall, list: { key: string; title: string }[]): Outcome => ({
    ok: false,
    failure: { kind: 'ambiguous', detail: { candidates: list, call } },
  });

  // The point a call means: "that" is the last touched, a phrase is matched against the visible points.
  function pointOf(call: ParsedCall): RetroPointDto | Outcome {
    const spoken = str(call.args.point);
    if (isThat(spoken)) return touched ?? ask(call, candidates);
    const found = matchPoint(spoken, ctx.points);
    if (found.length === 1) return found[0];
    if (found.length > 1)
      return ask(
        call,
        found.map((p) => ({ key: p.workItem?.key ?? '', title: p.text })),
      );
    return { ok: false, failure: unmatched(spoken, 'point', { kind: 'none' }) };
  }
  const isOutcome = (v: RetroPointDto | Outcome): v is Outcome => 'ok' in v;

  // A dictated point needs a start phrase before the note ("retro note", "add a point" or a category name), so the command is never in it.
  const START_PHRASES = ['retro note', 'add a point', ...Object.values(CATEGORY_LABELS)].map((p) =>
    p.toLowerCase(),
  );
  const hasStartPhrase = (lead: string): boolean => {
    const heard = ` ${lead.toLowerCase().replace(/\bcolon\b/gu, ':')} `;
    return START_PHRASES.some((p) =>
      new RegExp(`[^\\p{L}\\p{N}]${p}[^\\p{L}\\p{N}]`, 'u').test(heard),
    );
  };
  function noteOf(call: ParsedCall, transcript: Transcript): string | VoiceFailure {
    const span = call.noteSpan;
    const text = span === undefined ? null : copyNoteSpan(transcript, span);
    const lead = span === undefined ? '' : transcript.text.slice(0, span.start);
    return text === null || !hasStartPhrase(lead)
      ? { kind: 'unclear', detail: { heard: transcript.text } }
      : text;
  }

  // The reason is the transcript's own words (never the model's wording), or empty: a reason is never required (P-R4).
  function reasonOf(call: ParsedCall, transcript: Transcript): string | VoiceFailure {
    const said = str(call.args.reason)
      .trim()
      .replace(/[\s.]+$/u, '');
    if (said === '') return '';
    const at = transcript.text.toLowerCase().indexOf(said.toLowerCase());
    return at < 0
      ? { kind: 'unclear', detail: { heard: transcript.text } }
      : transcript.text.slice(at, at + said.length);
  }

  function addPoint(call: ParsedCall, transcript: Transcript, said: boolean): Outcome {
    const text = noteOf(call, transcript);
    if (typeof text !== 'string') return { ok: false, failure: text };
    const picked = call.args.category;
    if (!isCategory(picked)) return refuse(call.action, 'invalidArgs');
    const about = aboutOf(text, ctx.projectKey, ctx.sprint?.id ?? null);
    // A point about neither an item nor a known sprint cannot be sent, so it is never previewed.
    if (about.key === null && about.sprintId === null) return refuse(call.action, 'invalidArgs');
    return {
      ok: true,
      view: null,
      actions: [
        {
          area: 'retro',
          kind: 'addPoint',
          itemId: null,
          key: about.key,
          projectId: ctx.projectId,
          sprintId: about.sprintId,
          category: picked,
          categoryGuessed: !said,
          text,
        },
      ],
    };
  }

  function resolveOne(call: ParsedCall, transcript: Transcript): Outcome {
    const a = call.args;
    switch (call.action) {
      case 'add_point':
        return addPoint(call, transcript, false);
      case 'add_point_in':
        return addPoint(call, transcript, true);
      case 'support': {
        const p = pointOf(call);
        if (isOutcome(p)) return p;
        return {
          ok: true,
          view: null,
          actions: [{ area: 'retro', kind: 'support', itemId: null, key: null, pointId: p.id }],
        };
      }
      case 'make_action':
      case 'add_to_action':
      case 'not_needed': {
        const p = pointOf(call);
        if (isOutcome(p)) return p;
        if (p.category === 'wentWell') return refuse(call.action, 'notAllowed');
        if (call.action === 'add_to_action') {
          const spoken = str(a.action);
          const m = matchKey(
            spoken,
            ctx.openActions.map((x) => x.key),
          );
          if (m.kind !== 'match') return { ok: false, failure: unmatched(spoken, 'key', m) };
          const target = ctx.openActions.find((x) => x.key === m.value);
          if (target === undefined) return refuse(call.action, 'invalidArgs');
          return {
            ok: true,
            view: null,
            actions: [
              {
                area: 'retro',
                kind: 'addToAction',
                itemId: target.workItemId,
                key: target.key,
                projectId: ctx.projectId,
                pointIds: [p.id],
              },
            ],
          };
        }
        const sprintId = p.sprint?.id ?? ctx.sprint?.id ?? null;
        if (sprintId === null) return refuse(call.action, 'invalidArgs');
        if (call.action === 'not_needed') {
          const reason = reasonOf(call, transcript);
          if (typeof reason !== 'string') return { ok: false, failure: reason };
          return {
            ok: true,
            view: null,
            actions: [
              {
                area: 'retro',
                kind: 'notNeeded',
                itemId: null,
                key: null,
                projectId: ctx.projectId,
                sprintId,
                pointId: p.id,
                reason,
              },
            ],
          };
        }
        let owner: { id: string; name: string } | null = null;
        if (a.person !== undefined) {
          const spoken = str(a.person);
          const m = matchPerson(spoken, people);
          if (m.kind !== 'match') return { ok: false, failure: unmatched(spoken, 'person', m) };
          owner = m.value;
        }
        return {
          ok: true,
          view: null,
          actions: [
            {
              area: 'retro',
              kind: 'newAction',
              itemId: null,
              key: null,
              projectId: ctx.projectId,
              sprintId,
              pointIds: [p.id],
              title: seedTitle(p.text),
              ownerId: owner?.id ?? null,
              ownerName: owner?.name ?? null,
              themeId: p.theme?.id ?? null,
            },
          ],
        };
      }
      case 'answer_merge': {
        const answer = a.answer;
        if (answer !== 'merge' && answer !== 'keepApart') return refuse(call.action, 'invalidArgs');
        const list = ctx.proposals.map((p) => ({
          key: '',
          title: `${p.from.name} into ${p.into.name}`,
        }));
        if (ctx.proposals.length === 0) return refuse(call.action, 'notAllowed');
        let chosen: RetroMergeProposalDto | undefined;
        if (a.proposal === undefined) {
          if (ctx.proposals.length !== 1) return ask(call, list);
          chosen = ctx.proposals[0];
        } else {
          const spoken = str(a.proposal).toLowerCase();
          const found = ctx.proposals.filter(
            (p) =>
              spoken.includes(p.from.name.toLowerCase()) ||
              spoken.includes(p.into.name.toLowerCase()),
          );
          if (found.length > 1)
            return ask(
              call,
              found.map((p) => ({ key: '', title: `${p.from.name} into ${p.into.name}` })),
            );
          if (found.length === 0)
            return { ok: false, failure: unmatched(str(a.proposal), 'proposal', { kind: 'none' }) };
          chosen = found[0];
        }
        return {
          ok: true,
          view: null,
          actions: [
            {
              area: 'retro',
              kind: answer === 'merge' ? 'mergeThemes' : 'keepApart',
              itemId: null,
              key: null,
              projectId: ctx.projectId,
              proposalId: chosen.id,
              fromName: chosen.from.name,
              intoName: chosen.into.name,
            },
          ],
        };
      }
      case 'show': {
        const view = a.view;
        if (!isCategory(view) && view !== 'toDecide' && view !== 'features')
          return refuse(call.action, 'invalidArgs');
        const args: RetroViewArgs = { view: view satisfies RetroView };
        return { ok: true, actions: [], view: { kind: 'screen', command: 'show', args } };
      }
      case 'ask': {
        const topic = a.topic;
        if (topic !== 'keepsGoingWrong' && topic !== 'leftToDecide' && topic !== 'whatWentWell')
          return refuse(call.action, 'invalidArgs');
        return {
          ok: true,
          actions: [],
          view: {
            kind: 'answer',
            content: retroAnswer(topic satisfies RetroAskTopic, ctx.points),
          },
        };
      }
      default:
        return refuse(call.action, 'none');
    }
  }

  const lineFor = (action: RetroAction): PreviewLine => {
    const line = retroExecutor.describe(action);
    if (action.kind === 'support') {
      const p = ctx.points.find((x) => x.id === action.pointId);
      return { ...line, text: `+1 on "${p?.text ?? 'a point'}"`, removable: true };
    }
    if (action.kind === 'addToAction') {
      const p = ctx.points.find((x) => x.id === action.pointIds[0]);
      return p === undefined ? line : { ...line, detail: `"${p.text}"` };
    }
    return line;
  };

  return {
    screen: 'retro',
    actions: RETRO_ACTIONS,
    context: () => ({
      // The keys the correction sheets read: the points on screen in order, no states, the point last touched.
      rows: ctx.points.map((p) => ({ key: p.workItem?.key ?? '', title: p.text })),
      states: [],
      key: touched?.workItem?.key ?? null,
      sprint: ctx.sprint?.name ?? null,
      category: ctx.category === null ? null : CATEGORY_LABELS[ctx.category],
      points: ctx.points.map((p) => ({
        text: p.text,
        category: CATEGORY_LABELS[p.category],
        decision: p.decision,
        recurring: p.recurring,
      })),
      lastTouched: touched?.text ?? null,
      actions: ctx.openActions.map((x) => ({ key: x.key, title: x.title })),
      proposals: ctx.proposals.map((p) => ({ from: p.from.name, into: p.into.name })),
      people: people.map((p) => p.name),
    }),
    hotwords: () => [
      ...RETRO_CATEGORIES.map((c) => CATEGORY_LABELS[c]),
      'plus one',
      'not needed',
      'merge',
      'keep apart',
      ...ctx.openActions.map((x) => x.key),
      ...people.map((p) => p.name),
    ],
    toActions(calls, transcript): ResolveResult<RetroAction> {
      if (calls.length === 0)
        return { ok: false, failure: { kind: 'unclear', detail: { heard: transcript.text } } };
      const actions: RetroAction[] = [];
      const lines: PreviewLine[] = [];
      const rejected: { call: ParsedCall; failure: VoiceFailure }[] = [];
      let view: ViewIntent | null = null;
      for (const call of calls) {
        const out = resolveOne(call, transcript);
        if (!out.ok) {
          rejected.push({ call, failure: out.failure });
          continue;
        }
        for (const action of out.actions) {
          actions.push(action);
          lines.push(lineFor(action));
        }
        view ??= out.view;
      }
      if (actions.length === 0 && view === null) return { ok: false, failure: rejected[0].failure };
      return { ok: true, actions, lines, rejected, view };
    },
  };
}
