import type { AttachmentDto } from '@shiplean/core/api/attachments';
import { formatSize } from '../../workItem/side/fileSize';

/** "14 KB · uploaded by Claude Code (agent) through attach_markdown, 24 Oct · replaced twice", from the DTO alone. */
export function attachmentMeta(attachment: AttachmentDto): string {
  const who = attachment.agentName
    ? `${attachment.agentName} (agent)`
    : (attachment.uploadedBy?.name ?? 'Jira import');
  const through = attachment.via ? ` through ${attachment.via}` : '';
  const parts = [
    formatSize(attachment.sizeBytes),
    `uploaded by ${who}${through}, ${attachment.createdLabel}`,
  ];
  if (attachment.replacedCount > 0) parts.push(replaced(attachment.replacedCount));
  return parts.join(' · ');
}

function replaced(count: number): string {
  if (count === 1) return 'replaced once';
  if (count === 2) return 'replaced twice';
  return `replaced ${count} times`;
}

/** The About this file facts as label–value rows: Size, Uploaded by, When, On (`onItem` is "SHIP-101 Checkout v2"). */
export function attachmentFacts(
  attachment: AttachmentDto,
  onItem: string,
): [label: string, value: string][] {
  const who = attachment.agentName
    ? `${attachment.agentName} (agent)`
    : (attachment.uploadedBy?.name ?? 'Jira import');
  const when =
    attachment.replacedCount > 0
      ? `${attachment.createdLabel} · ${replaced(attachment.replacedCount)}`
      : attachment.createdLabel;
  return [
    ['Size', formatSize(attachment.sizeBytes)],
    ['Uploaded by', attachment.via ? `${who} through ${attachment.via}` : who],
    ['When', when],
    ['On', onItem],
  ];
}
