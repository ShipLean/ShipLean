import type { AttachmentDto, AttachmentKind } from '../../../api/attachments';

export const attachmentGroups: { kind: AttachmentKind; title: string; hint: string }[] = [
  { kind: 'markdown', title: 'Markdown', hint: 'opens rendered' },
  { kind: 'image', title: 'Images', hint: 'preview' },
  { kind: 'file', title: 'Files', hint: 'download' },
];

/** The limits the server enforces by default; the note under the rail and the drop overlay quote them. */
export const limitsNote = 'Up to 25 MB a file and 250 MB a Feature.';

export const ofKind = (list: AttachmentDto[], kind: AttachmentKind) =>
  list.filter((attachment) => attachment.kind === kind);
