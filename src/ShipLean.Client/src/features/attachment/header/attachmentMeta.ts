import type { AttachmentDto } from '../../../api/attachments';
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
