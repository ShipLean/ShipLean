// Comments on a work item. C#: ShipLean.Contracts.Comments. Nothing is omitted: absent values are null; timestamps are
// ISO strings and labels are formatted by the server. Archived comments are never returned.
import type { UserRef } from './refs';

export interface CommentDto {
  id: string;
  workItemId: string;
  /** Null for an imported comment whose author has no user. */
  author: UserRef | null;
  body: string;
  bodyHtml: string;
  createdAt: string;
  createdLabel: string;
  /** Null until the comment is edited. */
  editedAt: string | null;
  editedLabel: string | null;
  /** The agent that wrote it for the author ("Claude Code (claude.ai)" / "Claude Code (unverified)"); null when a person did. */
  agentName: string | null;
  /** Null until any member marks the comment superseded: kept, struck through, with who and when. */
  superseded: CommentSuperseded | null;
}

export interface CommentSuperseded {
  at: string;
  atLabel: string;
  by: UserRef;
}

export interface CommentCreate {
  body: string;
}

export interface CommentEdit {
  body: string;
}
