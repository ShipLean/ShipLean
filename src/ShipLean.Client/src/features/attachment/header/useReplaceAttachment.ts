import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import type { AttachmentDto } from '../../../api/attachments';
import { showToast } from '../../../components/toastStore';
import { routePath } from '../../../routes';
import { uploadAttachment } from '../../workItem/side/attachmentUpload';
import { attachmentMarkdownKey } from '../../feature/featureApi';

// Replace… and Save: upload a file to the item. The same name replaces this file (the server soft-deletes the old copy,
// so the page moves to the new one); another name adds a new attachment, and the toast says so.
export function useReplaceAttachment(
  projectId: string,
  projectKey: string,
  itemKey: string,
  attachmentId: string,
  workItemId: string,
) {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  return useMutation({
    mutationFn: (args: { file: File; currentName: string; savedText?: boolean }) =>
      uploadAttachment(workItemId, args.file),
    onSuccess: (uploaded: AttachmentDto, { file, currentName, savedText }) => {
      const replaced = file.name === currentName;
      showToast(
        savedText
          ? `Saved ${currentName}. The old copy is soft-deleted.`
          : replaced
            ? `Replaced ${currentName}. The old copy is soft-deleted.`
            : `Uploaded as ${file.name}: a different name adds a new attachment`,
        { tone: 'ok' },
      );
      if (replaced && uploaded.id !== attachmentId) {
        void navigate(
          routePath('workItemAttachment', { key: projectKey, itemKey, attachmentId: uploaded.id }),
          { replace: true },
        );
        return undefined;
      }
      return queryClient.invalidateQueries({
        queryKey: attachmentMarkdownKey(projectId, attachmentId),
      });
    },
  });
}
