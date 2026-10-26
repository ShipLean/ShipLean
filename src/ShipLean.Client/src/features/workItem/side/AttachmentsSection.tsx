import { useRef } from 'react';
import { useQuery } from '@tanstack/react-query';
import type { ProjectDto } from '../../../api/projects';
import { workItemTestIds as ids } from '../testIds';
import { fetchAttachments, useWorkItem, workItemAttachmentsKey } from '../workItemApi';
import { AttachmentRow } from './AttachmentRow';
import { useAttachmentEvents } from './useAttachmentEvents';
import { useAttachmentWrites } from './useAttachmentWrites';

// The item's attachments, oldest first. Attaching and removing save at once; nothing here reads or writes the form's draft.
export function AttachmentsSection({ project, itemKey }: { project: ProjectDto; itemKey: string }) {
  const item = useWorkItem(project.id, itemKey).data;
  const workItemId = item?.id ?? '';
  const attachments = useQuery({
    queryKey: workItemAttachmentsKey(project.id, itemKey),
    queryFn: () => fetchAttachments(workItemId),
    enabled: item !== undefined,
  });
  useAttachmentEvents(project.id, itemKey, item?.id);
  const writes = useAttachmentWrites(project.id, itemKey, workItemId);
  const picker = useRef<HTMLInputElement>(null);
  if (!item) return null;
  const list = attachments.data ?? [];
  const error = writes.upload.error ?? writes.archive.error;
  return (
    <section
      data-testid={ids.attachments}
      aria-label="Attachments"
      className="grid content-start gap-2"
    >
      <div className="flex items-center gap-2">
        <span className="text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase">
          Attachments
        </span>
        <span
          data-testid={ids.attachmentsCount}
          className="text-[length:var(--text-xs)] text-ink-muted"
        >
          {list.length}
        </span>
        <button
          type="button"
          data-testid={ids.attachmentUpload}
          disabled={writes.upload.isPending}
          className="ml-auto h-6 rounded-control px-2 text-[length:var(--text-xs)] text-signal hover:bg-signal-soft disabled:opacity-50"
          onClick={() => picker.current?.click()}
        >
          {writes.upload.isPending ? 'Attaching…' : '+ Attach'}
        </button>
        <input
          ref={picker}
          data-testid={ids.attachmentFile}
          type="file"
          multiple
          aria-label="Attach files"
          className="hidden"
          onChange={(event) => {
            const files = Array.from(event.target.files ?? []);
            event.target.value = '';
            if (files.length > 0) writes.upload.mutate(files);
          }}
        />
      </div>
      {error ? (
        <p
          data-testid={ids.attachmentError}
          role="alert"
          className="m-0 text-[length:var(--text-sm)] text-alert"
        >
          {error.message}
        </p>
      ) : null}
      {attachments.isError ? (
        <p
          data-testid={ids.attachmentsLoadFailed}
          role="alert"
          className="m-0 text-[length:var(--text-sm)] text-alert"
        >
          The attachments could not be loaded.
        </p>
      ) : list.length === 0 && attachments.isSuccess ? (
        <span
          data-testid={ids.attachmentsEmpty}
          className="text-[length:var(--text-sm)] text-ink-muted"
        >
          No attachments.
        </span>
      ) : (
        <ul className="m-0 grid list-none overflow-hidden rounded-md border border-edge bg-surface p-0">
          {list.map((attachment) => (
            <AttachmentRow
              key={attachment.id}
              attachment={attachment}
              onArchive={() => writes.archive.mutate(attachment)}
            />
          ))}
        </ul>
      )}
    </section>
  );
}
