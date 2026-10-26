import { useMutation, useQueryClient } from '@tanstack/react-query';
import type { AttachmentDto } from '../../../api/attachments';
import { showToast } from '../../../components/toastStore';
import { archiveAttachment, workItemAttachmentsKey } from '../workItemApi';
import { uploadAttachment } from './attachmentUpload';
import { formatSize } from './fileSize';

// An attachment is its own record and saves at once: these writes refetch the attachment list and never touch the item
// or the form's draft, so an attachment never makes the form dirty. Files go up one at a time; the toast names what got
// there, even when a later file in the same choice was refused.
export function useAttachmentWrites(projectId: string, itemKey: string, workItemId: string) {
  const queryClient = useQueryClient();
  const refetch = () =>
    queryClient.invalidateQueries({ queryKey: workItemAttachmentsKey(projectId, itemKey) });
  return {
    upload: useMutation({
      mutationFn: async (files: File[]) => {
        const attached: File[] = [];
        try {
          for (const file of files) {
            await uploadAttachment(workItemId, file);
            attached.push(file);
          }
        } finally {
          if (attached.length > 0) showToast(uploadedMessage(attached), { tone: 'ok' });
        }
      },
      onSettled: refetch,
    }),
    archive: useMutation({
      mutationFn: (attachment: AttachmentDto) => archiveAttachment(attachment.id),
      onSuccess: (_, attachment) => {
        showToast(`Removed ${attachment.name}`, { tone: 'ok' });
        return refetch();
      },
    }),
  };
}

function uploadedMessage(files: File[]): string {
  const [only] = files;
  return files.length === 1 && only
    ? `Uploaded ${only.name} (${formatSize(only.size)})`
    : `Uploaded ${files.length} files`;
}
