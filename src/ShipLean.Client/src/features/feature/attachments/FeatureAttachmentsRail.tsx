import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import type { AttachmentDto } from '../../../api/attachments';
import { useProjectByKey } from '../../../realtime/useProjectByKey';
import { useAttachmentEvents } from '../../workItem/side/useAttachmentEvents';
import { useAttachmentWrites } from '../../workItem/side/useAttachmentWrites';
import { workItemTestIds } from '../../workItem/testIds';
import { fetchAttachments, workItemAttachmentsKey } from '../../workItem/workItemApi';
import { useChooseFiles } from '../drop/dropZoneContext';
import type { FeatureRegionProps } from '../FeatureRegionProps';
import { featureTestIds as ids } from '../testIds';
import { attachmentGroups, limitsNote, ofKind } from './attachmentGroups';
import { ImagePreview } from './ImagePreview';
import { RailGroup } from './RailGroup';

// The Feature's attachments grouped by what opening them does. Attaching saves at once and never touches the draft.
export function FeatureAttachmentsRail({ item, projectKey }: FeatureRegionProps) {
  const project = useProjectByKey(projectKey).data;
  const attachments = useQuery({
    queryKey: workItemAttachmentsKey(item.projectId, item.key),
    queryFn: () => fetchAttachments(item.id),
  });
  useAttachmentEvents(item.projectId, item.key, item.id);
  const writes = useAttachmentWrites(item.projectId, item.key, item.id);
  const choose = useChooseFiles();
  const [previewing, setPreviewing] = useState<AttachmentDto | null>(null);
  const list = attachments.data ?? [];
  const error = writes.archive.error;
  return (
    <div data-testid={ids.attachmentsRail}>
      {project ? (
        <section
          data-testid={workItemTestIds.attachments}
          aria-label="Attachments"
          className="grid content-start gap-2"
        >
          <div className="flex items-center gap-2">
            <span className="text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase">
              Attachments
            </span>
            <span
              data-testid={ids.railCount}
              className="text-[length:var(--text-xs)] text-ink-muted"
            >
              {list.length}
            </span>
            <button
              type="button"
              data-testid={ids.railAttach}
              className="ml-auto h-6 rounded-control px-2 text-[length:var(--text-xs)] text-signal hover:bg-signal-soft"
              onClick={choose}
            >
              + Attach
            </button>
          </div>
          {error ? (
            <p role="alert" className="m-0 text-[length:var(--text-sm)] text-alert">
              {error.message}
            </p>
          ) : null}
          {attachments.isError ? (
            <p role="alert" className="m-0 text-[length:var(--text-sm)] text-alert">
              The attachments could not be loaded.
            </p>
          ) : list.length === 0 && attachments.isSuccess ? (
            <span
              data-testid={ids.railEmpty}
              className="text-[length:var(--text-sm)] text-ink-muted"
            >
              No attachments.
            </span>
          ) : (
            <div className="grid overflow-hidden rounded-md border border-edge bg-surface">
              {attachmentGroups.map(({ kind, title, hint }) => {
                const ofThisKind = ofKind(list, kind);
                return ofThisKind.length === 0 ? null : (
                  <RailGroup
                    key={kind}
                    kind={kind}
                    title={title}
                    hint={hint}
                    attachments={ofThisKind}
                    projectKey={projectKey}
                    itemKey={item.key}
                    onPreview={setPreviewing}
                    onRemove={(attachment) => writes.archive.mutate(attachment)}
                  />
                );
              })}
            </div>
          )}
          <p className="m-0 text-[length:var(--text-sm)] text-ink-muted">
            Same name again replaces the file; the old copy is soft-deleted. {limitsNote}
          </p>
          {previewing ? (
            <ImagePreview attachment={previewing} onClose={() => setPreviewing(null)} />
          ) : null}
        </section>
      ) : null}
    </div>
  );
}
