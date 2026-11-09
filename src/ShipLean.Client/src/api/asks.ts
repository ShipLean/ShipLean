// Decisions and questions on work items. C#: ShipLean.Contracts.Asks. Nothing is omitted on the wire: absent values are
// null and lists are []. Timestamps are ISO 8601 strings with a UTC offset, each next to a server-formatted label.
import type { UserRef, WorkItemTypeId } from './refs';

export const DECISION_STATES = ['open', 'confirmed', 'changed', 'withdrawn'] as const;
export type DecisionState = (typeof DECISION_STATES)[number];

export const QUESTION_STATES = ['open', 'answered', 'withdrawn'] as const;
export type QuestionState = (typeof QUESTION_STATES)[number];

/** Which answer a decision holds: the recommended one, one of its alternatives, or the owner's own words. */
export const ANSWER_KINDS = ['recommended', 'alternative', 'own'] as const;
export type AnswerKind = (typeof ANSWER_KINDS)[number];

export const ASK_EVENT_KINDS = [
  'raised',
  'edited',
  'confirmed',
  'changed',
  'withdrawn',
  'asked',
  'answered',
] as const;
export type AskEventKind = (typeof ASK_EVENT_KINDS)[number];

/** answer: the comment answers a question; changed: a settled decision was changed; about: it discusses a decision. */
export const ASK_TAG_KINDS = ['answer', 'changed', 'about'] as const;
export type AskTagKind = (typeof ASK_TAG_KINDS)[number];

/** The decisions and questions on one work item. */
export interface ItemAsksDto {
  workItemId: string;
  /** The assignee, or the creator when nobody is assigned (ownerIsCreator). */
  owner: UserRef | null;
  ownerIsCreator: boolean;
  ownerIsMe: boolean;
  openQuestions: number;
  openDecisions: number;
  questions: QuestionDto[];
  decisions: DecisionDto[];
  /** A Feature's Stories and Bugs with open asks; [] for any other type. */
  waitingChildren: WaitingChildDto[];
}

export interface QuestionDto {
  id: string;
  number: number;
  /** "Q1". */
  label: string;
  text: string;
  state: QuestionState;
  askedBy: UserRef;
  /** The agent that asked it for askedBy; null when a person did. */
  agentName: string | null;
  askedAt: string;
  askedAtLabel: string;
  /** Null until answered. */
  answer: QuestionAnswerDto | null;
  withdrawnAt: string | null;
  withdrawnAtLabel: string | null;
}

/** The comment that answered a question. */
export interface QuestionAnswerDto {
  commentId: string;
  /** Markdown. */
  body: string;
  bodyHtml: string;
  by: UserRef | null;
  agentName: string | null;
  at: string;
  atLabel: string;
}

export interface DecisionDto {
  id: string;
  number: number;
  /** "D3". */
  label: string;
  question: string;
  recommended: string;
  alternatives: string[];
  /** The answer in force: the recommended one until it is changed. */
  currentAnswer: string;
  answerKind: AnswerKind;
  /** Index into alternatives when answerKind is alternative, otherwise null. */
  alternativeIndex: number | null;
  state: DecisionState;
  raisedBy: UserRef;
  raisedAgentName: string | null;
  raisedAt: string;
  raisedAtLabel: string;
  /** Null until confirmed, changed or withdrawn. */
  answeredBy: UserRef | null;
  answeredAgentName: string | null;
  answeredAt: string | null;
  answeredAtLabel: string | null;
  /** Backfilled from a D-table in the description. */
  fromDescription: boolean;
  /** Oldest first. */
  history: DecisionEventDto[];
}

export interface DecisionEventDto {
  kind: AskEventKind;
  from: string | null;
  to: string | null;
  toKind: AnswerKind | null;
  note: string | null;
  by: UserRef | null;
  agentName: string | null;
  at: string;
  atLabel: string;
  fromDescription: boolean;
}

export interface WaitingChildDto {
  id: string;
  key: string;
  type: WorkItemTypeId;
  title: string;
  owner: UserRef | null;
  ownerIsCreator: boolean;
  openQuestions: number;
  openDecisions: number;
}

/** Everything waiting on the signed-in person. Items are oldest open ask first. */
export interface WaitingDto {
  openQuestions: number;
  openDecisions: number;
  items: WaitingItemDto[];
}

export interface WaitingItemDto {
  projectId: string;
  projectKey: string;
  projectName: string;
  id: string;
  key: string;
  type: WorkItemTypeId;
  title: string;
  openQuestions: number;
  openDecisions: number;
  oldestAt: string;
  oldestAtLabel: string;
}

/** One ask line in a work item's History. */
export interface AskHistoryLineDto {
  kind: AskEventKind;
  /** "D3", "Q1", or a run raised together, "D1–D5" (en dash). */
  badge: string;
  from: string | null;
  to: string | null;
  note: string | null;
  by: UserRef | null;
  agentName: string | null;
  at: string;
  atLabel: string;
}

/** What a comment says about an ask, shown as a chip in Discussion. */
export interface CommentAskTagDto {
  kind: AskTagKind;
  /** "Q1" or "D3". */
  label: string;
}

/** Numbers no longer open are skipped. */
export interface ConfirmDecisionsRequest {
  numbers: number[];
}

/** alternativeIndex for an alternative, text for the owner's own answer; note is an optional reason. */
export interface ChangeDecisionRequest {
  kind: AnswerKind;
  alternativeIndex: number | null;
  text: string | null;
  note: string | null;
}

export interface AnswerQuestionRequest {
  /** Markdown. */
  body: string;
}

export interface DiscussDecisionRequest {
  /** Markdown. */
  body: string;
}
