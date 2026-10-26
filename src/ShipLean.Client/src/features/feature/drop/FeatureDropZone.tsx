import { useRef, useState, type DragEvent, type ReactNode } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import type { FeatureRegionProps } from '../FeatureRegionProps';
import { checkUploads } from '../featureApi';
import { featureTestIds as ids } from '../testIds';
import { uploadAttachment } from '../../workItem/side/attachmentUpload';
import { workItemAttachmentsKey } from '../../workItem/workItemApi';
import { DropZoneContext } from './dropZoneContext';
import { DropOverlay, isUploadable, type DropEntry } from './DropOverlay';

const carriesFiles = (event: DragEvent) => event.dataTransfer.types.includes('Files');

// Files dropped anywhere on the form (or chosen on the hidden drop input) are checked first; the overlay shows each
// file's outcome, and Upload sends the ones the check allows. Attachments save at once and never touch the draft.
export function FeatureDropZone({ item, children }: FeatureRegionProps & { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [entries, setEntries] = useState<DropEntry[] | null>(null);
  const [dragging, setDragging] = useState(false);
  const [uploading, setUploading] = useState(false);
  const input = useRef<HTMLInputElement>(null);
  const [checkError, setCheckError] = useState<string | null>(null);

  const offer = async (files: File[]) => {
    if (files.length === 0) return;
    setCheckError(null);
    try {
      const checked = await checkUploads(item.id, {
        files: files.map((file) => ({
          name: file.name,
          sizeBytes: file.size,
          contentType: file.type,
        })),
      });
      setEntries(
        files.map((file, index) => ({
          file,
          outcome: checked.files[index]?.outcome ?? 'add',
          message: checked.files[index]?.message ?? '',
          error: null,
        })),
      );
    } catch (error) {
      setCheckError(error instanceof Error ? error.message : 'The files could not be checked.');
    }
  };

  const upload = async () => {
    if (!entries) return;
    setUploading(true);
    const after: DropEntry[] = [];
    for (const entry of entries) {
      if (!isUploadable(entry)) {
        after.push(entry);
        continue;
      }
      try {
        await uploadAttachment(item.id, entry.file);
      } catch (error) {
        after.push({
          ...entry,
          error: error instanceof Error ? error.message : 'The upload failed.',
        });
      }
    }
    setUploading(false);
    await queryClient.invalidateQueries({
      queryKey: workItemAttachmentsKey(item.projectId, item.key),
    });
    // Nothing is left to fix when every allowed file went up and the rest were only refused by the check.
    setEntries(after.some((entry) => entry.error) ? after.filter((entry) => entry.error) : null);
  };

  return (
    <div
      data-testid={ids.dropZone}
      className={dragging ? 'outline-2 -outline-offset-2 outline-signal outline-dashed' : undefined}
      onDragOver={(event) => {
        if (!carriesFiles(event)) return;
        event.preventDefault();
        setDragging(true);
      }}
      onDragLeave={(event) => {
        if (event.currentTarget === event.target) setDragging(false);
      }}
      onDrop={(event) => {
        if (!carriesFiles(event)) return;
        event.preventDefault();
        setDragging(false);
        void offer(Array.from(event.dataTransfer.files));
      }}
    >
      <input
        ref={input}
        data-testid={ids.dropInput}
        type="file"
        multiple
        aria-label="Drop files to attach"
        className="hidden"
        onChange={(event) => {
          const files = Array.from(event.target.files ?? []);
          event.target.value = '';
          void offer(files);
        }}
      />
      {checkError ? (
        <p
          role="alert"
          data-testid={ids.dropError}
          className="m-0 text-[length:var(--text-sm)] text-alert"
        >
          {checkError}
        </p>
      ) : null}
      <DropZoneContext value={() => input.current?.click()}>{children}</DropZoneContext>
      {entries ? (
        <DropOverlay
          itemKey={item.key}
          entries={entries}
          uploading={uploading}
          onUpload={() => void upload()}
          onCancel={() => setEntries(null)}
        />
      ) : null}
    </div>
  );
}
