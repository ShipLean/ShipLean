import { useMutation, useQueryClient } from '@tanstack/react-query';
import { showToast } from '../../../components/toastStore';
import { uploadAttachment } from '../../workItem/side/attachmentUpload';
import { attachmentMarkdownKey } from '../../feature/featureApi';

// Replace…: uploads the picked file to the item. The same name replaces this file (the server soft-deletes the old copy);
// another name adds a new attachment, and the toast says so. Either way the page reads the file again.
export function useReplaceAttachment(projectId: string, attachmentId: string, workItemId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (args: { file: File; currentName: string }) =>
      uploadAttachment(workItemId, args.file),
    onSuccess: (_, { file, currentName }) => {
      showToast(
        file.name === currentName
          ? `Replaced ${currentName}. The old copy is soft-deleted.`
          : `Uploaded as ${file.name}: a different name adds a new attachment`,
        { tone: 'ok' },
      );
      return queryClient.invalidateQueries({
        queryKey: attachmentMarkdownKey(projectId, attachmentId),
      });
    },
  });
}
