// Pure fake data and fake routes for the Work item screen (random UUIDs). The items follow
// design/mobileapp/work-item.html: SHIP-906, a Feature at risk, and SHIP-921, one of its Stories.
import type { DecisionDto, ItemAsksDto, QuestionDto } from '@shiplean/core/api/asks';
import type { CommentDto } from '@shiplean/core/api/comments';
import type { MemberDto, StateDto } from '@shiplean/core/api/projects';
import type { UserRef } from '@shiplean/core/api/refs';
import type { FeatureBoardViewDto } from '@shiplean/core/api/views';
import type { ChildSummaryDto, HistoryDto, WorkItemDto } from '@shiplean/core/api/workItems';
import members from '../../../ShipLean.TestSupport/DtoFixtures/projects/members.json';
import states from '../../../ShipLean.TestSupport/DtoFixtures/projects/states.json';
import commentFixtures from '../../../ShipLean.TestSupport/DtoFixtures/comments/comments.json';
import conflictHistory from '../../../ShipLean.TestSupport/DtoFixtures/workItems/conflict-history.json';
import featureBoard from '../../../ShipLean.TestSupport/DtoFixtures/views/feature-board-view.json';
import { aConflict, aProject, aWorkItem, aStory, aWriteResult } from './builders';
import { apiError, type ApiReply, type ApiTable, type FakeRequest } from './fakeApi';
import { controlledApi, type ControlledApi } from './controlledApi';
import { signedInApi } from './signedInApi';

const uuid = (): string => globalThis.crypto.randomUUID();

const person = (name: string): UserRef => ({
  id: uuid(),
  name,
  initials: name
    .split(' ')
    .map((part) => part[0])
    .join(''),
  avatarColour: '#5e81ac',
  photoUrl: null,
});

// SHIP-906: a Feature at risk, with one question waiting on a Story.
export function aFeatureItem(overrides: Partial<WorkItemDto> = {}): WorkItemDto {
  return aWorkItem({
    id: uuid(),
    key: 'SHIP-906',
    number: 906,
    title: 'Voice commands with on-device models',
    goal: 'Say what you want done and see it done, with no audio leaving the phone.',
    statusLine:
      'Parakeet runs at 1.4x real time on the S22; the 4 GB phone fallback is still open.',
    health: 'atRisk',
    rollups: [{ type: 'story', open: 7, closed: 2, inCurrentSprint: 5, inOtherSprint: 2 }],
    needsYou: { questions: 1, decisions: 1, onChildren: false },
    ...overrides,
  });
}

// SHIP-921: a Story under SHIP-906, with open and closed tasks.
export function aStoryItem(overrides: Partial<WorkItemDto> = {}): WorkItemDto {
  return aStory({
    id: uuid(),
    key: 'SHIP-921',
    number: 921,
    title: 'Pick the speech model per RAM tier',
    health: null,
    healthSet: null,
    rollups: [{ type: 'task', open: 2, closed: 3, inCurrentSprint: 2, inOtherSprint: 0 }],
    needsYou: null,
    ...overrides,
  });
}

export function aQuestion(overrides: Partial<QuestionDto> = {}): QuestionDto {
  return {
    id: uuid(),
    number: 1,
    label: 'Q1',
    text: 'Should the 4 GB phones use the small model?',
    options: ['Yes', 'No'],
    state: 'open',
    askedBy: person('Joel Dickson'),
    agentName: 'Claude Code (claude.ai)',
    askedAt: '2030-01-01T09:15:00+00:00',
    askedAtLabel: '1 Jan',
    answer: null,
    withdrawnAt: null,
    withdrawnAtLabel: null,
    ...overrides,
  };
}

export function aDecision(overrides: Partial<DecisionDto> = {}): DecisionDto {
  return {
    id: uuid(),
    number: 1,
    label: 'D1',
    question: 'Which model runs on a phone with 6 GB?',
    recommended: 'Qwen3 1.7B',
    alternatives: ['Qwen3 4B'],
    currentAnswer: 'Qwen3 1.7B',
    answerKind: 'recommended',
    alternativeIndex: null,
    state: 'open',
    raisedBy: person('Joel Dickson'),
    raisedAgentName: 'Claude Code (claude.ai)',
    raisedAt: '2030-01-01T09:00:00+00:00',
    raisedAtLabel: '1 Jan',
    answeredBy: null,
    answeredAgentName: null,
    answeredAt: null,
    answeredAtLabel: null,
    fromDescription: false,
    history: [],
    ...overrides,
  };
}

// The asks of one item. Counts are worked out from the lists unless overridden.
export function anItemAsks(
  itemId: string,
  parts: { decisions?: DecisionDto[]; questions?: QuestionDto[] } = {},
  overrides: Partial<ItemAsksDto> = {},
): ItemAsksDto {
  const decisions = parts.decisions ?? [];
  const questions = parts.questions ?? [];
  return {
    workItemId: itemId,
    owner: person('Joel Dickson'),
    ownerIsCreator: false,
    ownerIsMe: true,
    openQuestions: questions.filter((q) => q.state === 'open').length,
    openDecisions: decisions.filter((d) => d.state === 'open').length,
    questions,
    decisions,
    waitingChildren: [],
    ...overrides,
  };
}

export function aComment(itemId: string, overrides: Partial<CommentDto> = {}): CommentDto {
  return {
    ...(commentFixtures.comments[0] as CommentDto),
    id: uuid(),
    workItemId: itemId,
    ...overrides,
  };
}

export function aHistory(overrides: Partial<HistoryDto> = {}): HistoryDto {
  return { ...(conflictHistory.history as HistoryDto), ...overrides };
}

// SHIP-906's board: the fixture's rows, headed by the Feature, with the first story waiting on you.
export function aFeatureBoard(overrides: Partial<FeatureBoardViewDto> = {}): FeatureBoardViewDto {
  const board = structuredClone(featureBoard) as FeatureBoardViewDto;
  board.header = {
    ...board.header,
    key: 'SHIP-906',
    title: 'Voice commands with on-device models',
  };
  board.rows = board.rows.map((row, i) => ({
    ...row,
    needsYou: i === 0 ? { questions: 1, decisions: 0, onChildren: false } : null,
  }));
  return { ...board, ...overrides };
}

export interface WorkItemApiOptions {
  projectId?: string;
  // Defaults to SHIP-906 and SHIP-921.
  items?: WorkItemDto[];
  // Defaults to two open decisions and one open question on the first item, none on the rest.
  asks?: ItemAsksDto[];
  comments?: CommentDto[];
  history?: HistoryDto;
  board?: FeatureBoardViewDto;
  children?: ChildSummaryDto[];
}

// Comments the fake POST answers with, so the archive route (the Undo of a note) is a known path.
const noteId = (n: number): string => `00000008-0000-4000-8001-0000000000f${n}`;
// A fake allows nine posted notes (an archived one's id is not reused).
export const NOTE_IDS = [1, 2, 3, 4, 5, 6, 7, 8, 9].map(noteId);

const validation = (message: string, field: string): ApiReply => ({
  status: 400,
  body: { error: message, code: 'validation', field },
});

const blank = (body: unknown): boolean =>
  typeof (body as { body?: unknown } | undefined)?.body !== 'string' ||
  (body as { body: string }).body.trim() === '';

// Answers the by-key read, PATCH, comments (list, add, archive), history, asks (read and writes), the status confirm,
// children, the project's states and members and the by-key Feature board. A route in `table` replaces the default.
// It refuses what the server refuses: a stale revision (409 with the item as it is), an empty reply or note (400 on
// body), no decision numbers (400 on numbers), a settled decision or answered question (409), an unknown number or an
// archived comment (404). It fails, goes offline and hangs on demand because it is a controlledApi.
export function workItemApi(table: ApiTable = {}, opts: WorkItemApiOptions = {}): ControlledApi {
  const projectId = opts.projectId ?? aProject().id;
  const project = `/api/projects/${projectId}`;
  const feature = aFeatureItem({ projectId });
  const items = opts.items ?? [feature, aStoryItem({ projectId })];
  const asks = new Map<string, ItemAsksDto>(
    (opts.asks ?? items.map((item, i) => anItemAsks(item.id, i === 0 ? openAsks() : {}))).map(
      (a) => [a.workItemId, structuredClone(a)],
    ),
  );
  const revisions = new Map(items.map((item) => [item.id, item.revision]));
  const comments = new Map<string, CommentDto[]>(
    items.map((item, i) => [item.id, i === 0 ? (opts.comments ?? []) : []]),
  );
  const added = new Set<string>();
  let nextNote = 0;
  const board = opts.board ?? aFeatureBoard();
  const current = (item: WorkItemDto): WorkItemDto => ({
    ...item,
    revision: revisions.get(item.id) ?? item.revision,
  });
  // A write against a stale revision is a 409 with the item as it is; otherwise it bumps the revision.
  const write = (item: WorkItemDto, expected: unknown): ApiReply | null => {
    if (expected !== revisions.get(item.id)) {
      return { status: 409, body: aConflict(current(item)) };
    }
    revisions.set(item.id, (revisions.get(item.id) ?? 0) + 1);
    return null;
  };

  // Discussing a decision and answering a question each post a comment on the server too.
  const postComment = (itemId: string, body: string, id?: string): CommentDto => {
    const comment = aComment(itemId, {
      ...(id === undefined ? {} : { id }),
      body,
      bodyHtml: `<p>${body}</p>\n`,
    });
    comments.set(itemId, [...(comments.get(itemId) ?? []), comment]);
    return comment;
  };

  const routes: ApiTable = {
    ...signedInApi(),
    [`GET ${project}/states`]: states as StateDto[],
    [`GET ${project}/members`]: members as MemberDto[],
  };
  for (const item of items) {
    const path = `/api/work-items/${item.id}`;
    routes[`GET ${project}/work-items/by-key/${item.key}`] = () => current(item);
    routes[`GET ${project}/work-items/by-key/${item.key}/feature-board`] = board;
    routes[`PATCH ${path}`] = (request: FakeRequest) =>
      write(item, (request.body as { expectedRevision?: unknown }).expectedRevision) ??
      aWriteResult({ id: item.id, key: item.key, revision: revisions.get(item.id) });
    routes[`POST ${path}/status/confirm`] = (request: FakeRequest) =>
      write(item, (request.body as { revision?: unknown }).revision) ?? current(item);
    routes[`GET ${path}/children`] = (request: FakeRequest) => {
      const type = new URL(request.url, 'https://shiplean.test').searchParams.get('type');
      return (opts.children ?? item.children).filter((c) => type === null || c.type.id === type);
    };
    routes[`GET ${path}/history`] = opts.history ?? aHistory();
    routes[`GET ${path}/asks`] = () => {
      const a = structuredClone(asks.get(item.id));
      if (a === undefined) return apiError(404, 'not_found', 'That work item does not exist.');
      a.openDecisions = a.decisions.filter((d) => d.state === 'open').length;
      a.openQuestions = a.questions.filter((q) => q.state === 'open').length;
      return a;
    };
    routes[`GET ${path}/comments`] = () => comments.get(item.id);
    routes[`POST ${path}/comments`] = (request: FakeRequest) => {
      if (blank(request.body)) return apiError(400, 'validation', 'A comment cannot be empty.');
      if (nextNote >= NOTE_IDS.length) throw new Error('The fake posts at most 9 notes.');
      const comment = postComment(
        item.id,
        (request.body as { body: string }).body,
        NOTE_IDS[nextNote++],
      );
      added.add(comment.id);
      return comment;
    };
    routes[`POST ${path}/decisions/confirm`] = (request: FakeRequest) => {
      const numbers = (request.body as { numbers?: number[] }).numbers;
      if (numbers === undefined || numbers.length === 0)
        return validation('Choose at least one decision.', 'numbers');
      const unique = [...new Set(numbers)];
      for (const number of unique) {
        const decision = asks.get(item.id)?.decisions.find((d) => d.number === number);
        if (decision === undefined)
          return apiError(404, 'not_found', `D${number} does not exist on that work item.`);
        // One number must be open; with several, the server skips the ones no longer open.
        if (unique.length === 1 && decision.state !== 'open')
          return apiError(
            409,
            'conflict',
            `D${number} is ${decision.state}: only an open decision can be confirmed.`,
          );
      }
      for (const number of unique) settle(asks, item.id, number);
      return { status: 204 };
    };
    for (let number = 1; number <= 9; number++) {
      routes[`POST ${path}/decisions/${number}/discuss`] = (request: FakeRequest) => {
        if (blank(request.body)) return validation('Write something to post.', 'body');
        if (!asks.get(item.id)?.decisions.some((d) => d.number === number))
          return apiError(404, 'not_found', `D${number} does not exist on that work item.`);
        postComment(item.id, (request.body as { body: string }).body);
        return { status: 204 };
      };
      routes[`POST ${path}/questions/${number}/answer`] = (request: FakeRequest) => {
        if (blank(request.body)) return validation('Write an answer.', 'body');
        const question = asks.get(item.id)?.questions.find((q) => q.number === number);
        if (question === undefined)
          return apiError(404, 'not_found', `Q${number} does not exist on this work item.`);
        if (question.state !== 'open')
          return apiError(
            409,
            'conflict',
            `Q${number} is ${question.state}: only an open question can change.`,
          );
        question.state = 'answered';
        postComment(item.id, (request.body as { body: string }).body);
        return { status: 204 };
      };
    }
  }
  for (const id of NOTE_IDS) {
    routes[`POST /api/comments/${id}/archive`] = () => {
      if (!added.has(id)) return apiError(404, 'not_found', 'That comment does not exist.');
      added.delete(id);
      for (const [k, list] of comments)
        comments.set(
          k,
          list.filter((c) => c.id !== id),
        );
      return { status: 204 };
    };
  }
  return controlledApi({ ...routes, ...table });
}

function openAsks(): { decisions: DecisionDto[]; questions: QuestionDto[] } {
  return {
    decisions: [
      aDecision(),
      aDecision({
        number: 2,
        label: 'D2',
        question: 'Which model runs on a phone with 4 GB?',
        recommended: 'Qwen3 0.6B',
        alternatives: [],
        currentAnswer: 'Qwen3 0.6B',
      }),
    ],
    questions: [aQuestion()],
  };
}

function settle(asks: Map<string, ItemAsksDto>, itemId: string, number: number): void {
  const decision = asks.get(itemId)?.decisions.find((d) => d.number === number);
  if (decision !== undefined) decision.state = 'confirmed';
}
