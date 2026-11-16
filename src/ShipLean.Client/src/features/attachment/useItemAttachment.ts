import { useQuery } from '@tanstack/react-query';
import type { AttachmentDto } from '@shiplean/core/api/attachments';
import {
  fetchAttachments,
  fetchWorkItem,
  workItemAttachmentsKey,
  workItemKey,
} from '../workItem/workItemApi';

/**
 * One attachment as the item's attachments list has it (name, size, who, when), for a page whose own query failed (a
 * file too large to render). Reads nothing until `enabled`.
 */
export function useItemAttachment(
  projectId: string,
  itemKey: string,
  attachmentId: string,
  enabled: boolean,
): AttachmentDto | undefined {
  const item = useQuery({
    queryKey: workItemKey(projectId, itemKey),
    queryFn: () => fetchWorkItem(projectId, itemKey),
    enabled,
  });
  const workItemId = item.data?.id;
  const list = useQuery({
    queryKey: workItemAttachmentsKey(projectId, itemKey),
    queryFn: () => fetchAttachments(workItemId ?? ''),
    enabled: enabled && workItemId !== undefined,
  });
  return list.data?.find((attachment) => attachment.id === attachmentId);
}
