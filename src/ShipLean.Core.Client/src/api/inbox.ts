// The phone app's Inbox: everything in a project waiting on the signed-in person, and what they answered. C#:
// ShipLean.Contracts.Inbox. Nothing is omitted on the wire: absent values are null and lists are []. Timestamps are ISO
// 8601 strings with a UTC offset, each next to a server-formatted label.
import type { UserRef, WorkItemTypeId } from './refs';

export const INBOX_ASK_KINDS = ['decision', 'question', 'mention', 'status'] as const;
export type InboxAskKind = (typeof INBOX_ASK_KINDS)[number];

/** agentPickedUp: an agent comment or revision came next; personReplied: another person commented. */
export const ANSWERED_NEXT_KINDS = ['agentPickedUp', 'personReplied'] as const;
export type AnsweredNextKind = (typeof ANSWERED_NEXT_KINDS)[number];

/** Everything in one project waiting on the signed-in person. */
export interface InboxDto {
  projectId: string;
  /** The asks over items: the tab badge. */
  openAsks: number;
  openItems: number;
  /** False until anything has ever waited on the person (the first-run empty state). */
  everWaited: boolean;
  /** The server's clock time of the read ("9:12"). */
  loadedAtLabel: string;
  /** In display order: longest waiting first, a Story's row under its Feature's. */
  items: InboxItemDto[];
  /** The person's other projects with their open-ask counts. */
  otherProjects: InboxOtherProjectDto[];
}

export interface InboxItemDto {
  id: string;
  key: string;
  type: WorkItemTypeId;
  title: string;
  revision: number;
  /** The parent Feature's key when that Feature's row is in the same list, otherwise null. */
  underKey: string | null;
  /** The earliest of its asks' at. */
  oldestAt: string;
  /** "14h", "4d", "3w". */
  ageLabel: string;
  longWait: boolean;
  /** At most 3 open items this one blocks; waitedOnByMore counts the rest. */
  waitedOnBy: InboxRefDto[];
  waitedOnByMore: number;
  /** Questions, decisions by number, mentions oldest first, then the status line. */
  asks: InboxAskDto[];
}

/** Exactly one of decision, question, mention and status is set, matching kind. */
export interface InboxAskDto {
  kind: InboxAskKind;
  /** "Q1", "D3", "@1", "@2" (mentions per item, oldest first) or "Status". */
  label: string;
  at: string;
  atLabel: string;
  /** Who raised it; for a status ask, who last verified it (null when never). */
  from: UserRef | null;
  agentName: string | null;
  decision: InboxDecisionDto | null;
  question: InboxQuestionDto | null;
  mention: InboxMentionDto | null;
  status: InboxStatusDto | null;
}

export interface InboxDecisionDto {
  number: number;
  question: string;
  recommended: string;
  alternatives: string[];
}

export interface InboxQuestionDto {
  number: number;
  text: string;
  /** Quick answers (at most 6); [] when none. */
  options: string[];
}

export interface InboxMentionDto {
  mentionId: string;
  commentId: string;
  /** Plain text, at most 280 characters, cut on a word with "…". */
  excerpt: string;
}

export interface InboxStatusDto {
  statusLine: string;
  /** Null when never verified. */
  verifiedAt: string | null;
}

export interface InboxRefDto {
  key: string;
  title: string;
}

export interface InboxOtherProjectDto {
  projectId: string;
  projectKey: string;
  openAsks: number;
}

/** What the person answered in the project in the last 7 days, newest first, at most 50 rows. */
export interface AnsweredDto {
  answeredToday: number;
  pickedUpToday: number;
  rows: AnsweredRowDto[];
}

export interface AnsweredRowDto {
  itemId: string;
  key: string;
  type: WorkItemTypeId;
  title: string;
  kind: InboxAskKind;
  /** "D1–D8", "Q1", "@". */
  labels: string;
  /** "Approved D1–D8", "Answered Q1", "Replied to Priya Shah", "Marked seen". */
  summary: string;
  answeredAt: string;
  answeredAtLabel: string;
  /** The first thing after the answer on that item; null when nothing has happened yet. */
  next: AnsweredNextDto | null;
}

export interface AnsweredNextDto {
  kind: AnsweredNextKind;
  /** The person, or the person an agent runs for. */
  by: UserRef | null;
  agentName: string | null;
  at: string;
  atLabel: string;
}

/** The body of POST /api/mentions/{id}/reply. Markdown; the server prefixes "@<their full name>, ". */
export interface MentionReplyRequest {
  body: string;
}
