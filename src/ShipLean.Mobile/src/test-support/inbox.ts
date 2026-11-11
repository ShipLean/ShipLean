// Pure Inbox fakes (no jest, no React Native): builders with random ids, a controlled fake API for every Inbox route,
// and fake actions and sheets that record what they are asked.
import type {
  AnsweredDto,
  AnsweredRowDto,
  InboxAskDto,
  InboxDecisionDto,
  InboxDto,
  InboxItemDto,
  InboxMentionDto,
  InboxQuestionDto,
  InboxStatusDto,
} from '@shiplean/core/api/inbox';
import type { UserRef } from '@shiplean/core/api/refs';
import answeredFixture from '../../../ShipLean.TestSupport/DtoFixtures/inbox/answered.json';
import type { InboxAction, PendingAnswer } from '../inbox/actions';
import type { InboxActions } from '../inbox/InboxActions';
import type { AnswerSheetRequest, InboxSheets } from '../inbox/InboxSheets';
import { aProject, aWorkItem, aWriteResult } from './builders';
import { controlledApi, type ControlledApi } from './controlledApi';
import type { ApiTable } from './fakeApi';
import { signedInApi } from './signedInApi';

const uuid = (): string => globalThis.crypto.randomUUID();

let nextKey = 500;
const aKey = () => `SHIP-${(nextKey += 1)}`;

export function aUser(name = 'Priya Shah', initials = 'PS', avatarColour = '#2f7fd8'): UserRef {
  return { id: uuid(), name, initials, avatarColour, photoUrl: null };
}

const AT = '2026-11-04T10:05:00+00:00';

const ask = (overrides: Partial<InboxAskDto>): InboxAskDto => ({
  kind: 'decision',
  label: 'D1',
  at: AT,
  atLabel: '4 Nov',
  from: aUser(),
  agentName: null,
  decision: null,
  question: null,
  mention: null,
  status: null,
  ...overrides,
});

export function aDecisionAsk(
  decision: Partial<InboxDecisionDto> = {},
  overrides: Partial<InboxAskDto> = {},
): InboxAskDto {
  const d: InboxDecisionDto = {
    number: 1,
    question: 'Where do saved cards live?',
    recommended: 'In the payments service',
    alternatives: ['In the customer profile'],
    ...decision,
  };
  return ask({
    kind: 'decision',
    label: `D${d.number}`,
    agentName: 'Claude Code',
    decision: d,
    ...overrides,
  });
}

export function aQuestionAsk(
  question: Partial<InboxQuestionDto> = {},
  overrides: Partial<InboxAskDto> = {},
): InboxAskDto {
  const q: InboxQuestionDto = {
    number: 1,
    text: 'What size cap for demo videos?',
    options: [],
    ...question,
  };
  return ask({ kind: 'question', label: `Q${q.number}`, question: q, ...overrides });
}

// Labelled "@1" unless overrides give another label.
export function aMentionAsk(
  mention: Partial<InboxMentionDto> = {},
  overrides: Partial<InboxAskDto> = {},
): InboxAskDto {
  const m: InboxMentionDto = {
    mentionId: uuid(),
    commentId: uuid(),
    excerpt: '@Joel lane C needs the GitHub app installed on the org.',
    ...mention,
  };
  return ask({ kind: 'mention', label: '@1', mention: m, ...overrides });
}

export function aStatusAsk(
  status: Partial<InboxStatusDto> = {},
  overrides: Partial<InboxAskDto> = {},
): InboxAskDto {
  const s: InboxStatusDto = { statusLine: 'Card list built.', verifiedAt: AT, ...status };
  return ask({ kind: 'status', label: 'Status', status: s, ...overrides });
}

export function anInboxItem(overrides: Partial<InboxItemDto> = {}): InboxItemDto {
  return {
    id: uuid(),
    key: aKey(),
    type: 'feature',
    title: 'Saved cards at checkout',
    revision: 3,
    underKey: null,
    oldestAt: AT,
    ageLabel: '4d',
    longWait: true,
    waitedOnBy: [],
    waitedOnByMore: 0,
    asks: [aDecisionAsk()],
    ...overrides,
  };
}

// openAsks and openItems follow the items unless overridden. The project is aProject() (signedInApi's SHIP).
export function anInbox(overrides: Partial<InboxDto> = {}): InboxDto {
  const items = overrides.items ?? [anInboxItem()];
  return {
    projectId: aProject().id,
    openAsks: items.reduce((n, i) => n + i.asks.length, 0),
    openItems: items.length,
    everWaited: true,
    loadedAtLabel: '09:12',
    otherProjects: [],
    ...overrides,
    items,
  };
}

const answeredBase = answeredFixture as AnsweredDto;

export function anAnsweredRow(overrides: Partial<AnsweredRowDto> = {}): AnsweredRowDto {
  return { ...answeredBase.rows[1], itemId: uuid(), key: aKey(), next: null, ...overrides };
}

export function anAnswered(overrides: Partial<AnsweredDto> = {}): AnsweredDto {
  return { answeredToday: 1, pickedUpToday: 0, rows: [anAnsweredRow()], ...overrides };
}

export interface InboxApiOptions {
  // Defaults to the inbox's project.
  projectId?: string;
  inbox?: InboxDto;
  answered?: AnsweredDto;
}

// A fake server for the Inbox: signedInApi, both reads, and every write on the given inbox's asks in its final shape
// (204 for asks and mentions, 200 WorkItemDto for status confirm, 200 WriteResult for the status PATCH), with the
// controls of controlledApi (failNext, offline, hang). `table` adds or replaces routes.
export function inboxApi(table: ApiTable = {}, opts: InboxApiOptions = {}): ControlledApi {
  const inbox =
    opts.inbox ?? anInbox(opts.projectId === undefined ? {} : { projectId: opts.projectId });
  const projectId = opts.projectId ?? inbox.projectId;
  const project = `/api/projects/${projectId}`;
  const routes: ApiTable = {
    [`GET ${project}/inbox`]: inbox,
    [`GET ${project}/inbox/answered`]: opts.answered ?? anAnswered(),
  };
  const noContent = { status: 204 };
  for (const item of inbox.items) {
    const path = `/api/work-items/${item.id}`;
    for (const a of item.asks) {
      if (a.decision !== null) {
        const n = a.decision.number;
        routes[`POST ${path}/decisions/confirm`] = noContent;
        for (const write of ['change', 'withdraw', 'discuss'])
          routes[`POST ${path}/decisions/${n}/${write}`] = noContent;
      }
      if (a.question !== null) {
        const n = a.question.number;
        for (const write of ['answer', 'withdraw'])
          routes[`POST ${path}/questions/${n}/${write}`] = noContent;
      }
      if (a.mention !== null) {
        routes[`POST /api/mentions/${a.mention.mentionId}/seen`] = noContent;
        routes[`POST /api/mentions/${a.mention.mentionId}/reply`] = noContent;
      }
      if (a.status !== null) {
        const next = { id: item.id, key: item.key, revision: item.revision + 1 };
        routes[`POST ${path}/status/confirm`] = aWorkItem({
          ...next,
          title: item.title,
          statusLine: a.status.statusLine,
        });
        routes[`PATCH ${path}`] = aWriteResult(next);
      }
    }
  }
  return controlledApi({ ...signedInApi(), ...routes, ...table });
}

export interface FakeInboxActions extends InboxActions {
  readonly dispatched: InboxAction[];
  readonly undone: string[];
  readonly retried: string[];
}

// Records every call and sends nothing. Set pending before rendering.
export function fakeInboxActions(pending: PendingAnswer[] = []): FakeInboxActions {
  const dispatched: InboxAction[] = [];
  const undone: string[] = [];
  const retried: string[] = [];
  return {
    dispatched,
    undone,
    retried,
    pending,
    dispatch: (a) => {
      dispatched.push(a);
      return `batch-${dispatched.length}`;
    },
    undo: (batchId) => {
      undone.push(batchId);
    },
    retry: (id) => {
      retried.push(id);
    },
  };
}

export type SheetCall =
  | { kind: 'answer'; request: AnswerSheetRequest }
  | { kind: 'review'; item: InboxItemDto; scrollToConfirm: boolean; projectId: string }
  | { kind: 'close' };

export interface FakeInboxSheets extends InboxSheets {
  readonly calls: SheetCall[];
}

// Records every call and shows nothing.
export function fakeInboxSheets(): FakeInboxSheets {
  const calls: SheetCall[] = [];
  return {
    calls,
    openAnswer: (request) => {
      calls.push({ kind: 'answer', request });
    },
    openReview: (item, { scrollToConfirm, projectId }) => {
      calls.push({ kind: 'review', item, scrollToConfirm, projectId });
    },
    close: () => {
      calls.push({ kind: 'close' });
    },
  };
}
