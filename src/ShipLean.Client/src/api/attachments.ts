// Files on a work item. C#: ShipLean.Contracts.Attachments. Nothing is omitted: absent values are null; timestamps are
// ISO strings and labels are formatted by the server.
import type { MarkdownImageDto, MarkdownOutlineEntryDto } from './markdown';
import type { TypeRef, UserRef } from './refs';

export type AttachmentKind = 'markdown' | 'image' | 'file';

export interface AttachmentDto {
  id: string;
  workItemId: string;
  name: string;
  contentType: string;
  sizeBytes: number;
  /** Null for an import with no user. */
  uploadedBy: UserRef | null;
  createdAt: string;
  createdLabel: string;
  /** "/api/attachments/{id}/content". */
  downloadUrl: string;
  /** From the name and content type: `.md` or text/markdown, png/jpeg/gif/webp/svg, else file. */
  kind: AttachmentKind;
  /** How many earlier files of the same name this one replaced. */
  replacedCount: number;
  /** Set when an agent attached it; `via` is then the MCP tool name. */
  agentName: string | null;
  via: string | null;
}

export type AttachmentOutcome = 'add' | 'replace' | 'tooLarge' | 'overTotal';

/** Body of POST /api/work-items/{id}/attachments/check. */
export interface AttachmentCheckRequest {
  files: { name: string; sizeBytes: number; contentType: string }[];
}

/** One outcome per requested file, in request order. `message` is '' when the outcome needs none. */
export interface AttachmentCheckDto {
  files: { name: string; outcome: AttachmentOutcome; message: string }[];
}

/** GET /api/attachments/{id}/markdown. `siblings` are the item's other Markdown files. */
export interface AttachmentMarkdownDto {
  attachment: AttachmentDto;
  workItem: { id: string; key: string; title: string; type: TypeRef };
  markdown: string;
  html: string;
  outline: MarkdownOutlineEntryDto[];
  images: MarkdownImageDto[];
  siblings: AttachmentDto[];
}
