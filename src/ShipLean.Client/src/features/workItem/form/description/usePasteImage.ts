import { useEffect, useRef, useState, type ClipboardEvent } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { uploadAttachment } from '../../side/attachmentUpload';
import { fetchAttachments } from '../../workItemApi';
import { imageFiles, insertReferences, pastedNames } from './pastedImages';

/**
 * Pasting an image into the markdown textarea attaches it to the item at once (an attachment is its own record, never
 * part of the draft) and puts `![](name)` where the cursor was. A paste with no image is left to the browser. When an
 * upload fails nothing is inserted for it and `error` says why; images that went up before it are still inserted.
 */
export function usePasteImage({
  workItemId,
  attachmentsKey,
  value,
  onChange,
}: {
  workItemId: string | undefined;
  attachmentsKey: readonly unknown[] | undefined;
  value: string;
  onChange: (markdown: string) => void;
}) {
  const queryClient = useQueryClient();
  const [pasting, setPasting] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const latest = useRef({ value, onChange });
  useEffect(() => {
    latest.current = { value, onChange };
  });

  const onPaste =
    workItemId === undefined || attachmentsKey === undefined
      ? undefined
      : (event: ClipboardEvent<HTMLTextAreaElement>) => {
          const files = imageFiles(event.clipboardData.files);
          if (files.length === 0) return;
          event.preventDefault();
          const { selectionStart, selectionEnd } = event.currentTarget;
          setError(null);
          setPasting((count) => count + 1);
          void (async () => {
            const attached: string[] = [];
            try {
              const existing = await queryClient.fetchQuery({
                queryKey: attachmentsKey,
                queryFn: () => fetchAttachments(workItemId),
                staleTime: 0,
              });
              const names = pastedNames(
                files,
                existing.map((attachment) => attachment.name),
              );
              for (const [i, file] of files.entries()) {
                const name = names[i] as string;
                await uploadAttachment(workItemId, new File([file], name, { type: file.type }));
                attached.push(name);
              }
            } catch (failure) {
              setError(
                failure instanceof Error ? failure.message : 'That image could not be attached.',
              );
            } finally {
              if (attached.length > 0) {
                const next = insertReferences(
                  latest.current.value,
                  selectionStart,
                  selectionEnd,
                  attached,
                );
                latest.current.onChange(next.text);
                void queryClient.invalidateQueries({ queryKey: attachmentsKey });
              }
              setPasting((count) => count - 1);
            }
          })();
        };
  return { onPaste, pasting: pasting > 0, error };
}
