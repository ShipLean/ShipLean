// Rendered Markdown. C#: ShipLean.Contracts.Markdown. The HTML is sanitised on the server.

/** Attached: a bare name matching an image on the work item. External: an absolute URL, never loaded. Missing: anything else. */
export type MarkdownImageStatus = 'attached' | 'external' | 'missing';

/** An image the Markdown refers to, once per distinct `src`. `attachmentId` is set only when attached. */
export interface MarkdownImageDto {
  src: string;
  status: MarkdownImageStatus;
  attachmentId: string | null;
}

/** A heading: level 1-6, its plain text, and the id the rendered heading carries (link to it as `#anchor`). */
export interface MarkdownOutlineEntryDto {
  level: number;
  text: string;
  anchor: string;
}

/** Body of POST /api/markdown/render. `workItemId`, when given, is the item whose attachments the images are matched against. */
export interface MarkdownRenderRequest {
  markdown: string;
  workItemId?: string;
}

export interface RenderedMarkdownDto {
  markdown: string;
  html: string;
  images: MarkdownImageDto[];
}
