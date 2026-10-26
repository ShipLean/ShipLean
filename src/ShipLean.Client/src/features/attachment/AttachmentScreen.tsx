import { useState } from 'react';
import { ApiRequestError } from '../../api/client';
import type { AttachmentSlotProps } from '../../slots/AttachmentSlot';
import { useAttachmentMarkdown } from '../feature/featureApi';
import { AttachmentUnavailable } from './AttachmentUnavailable';
import { AttachmentBody } from './body/AttachmentBody';
import { AttachmentHeader } from './header/AttachmentHeader';
import type { AttachmentView } from './header/ViewToggle';
import { AttachmentSidePanel } from './side/AttachmentSidePanel';
import { attachmentTestIds as ids } from './testIds';

// One Markdown attachment on its own page (attachment.html): header and toolbar, the body rendered or as source, and the
// side panel. One request fills it; a replace or a teammate's change reads it again.
export function AttachmentScreen({ project, itemKey, attachmentId }: AttachmentSlotProps) {
  const [view, setView] = useState<AttachmentView>('rendered');
  const query = useAttachmentMarkdown(project.id, attachmentId);
  const data = query.data;

  let content;
  if (data) {
    content = (
      <>
        <AttachmentHeader
          projectId={project.id}
          projectKey={project.key}
          itemKey={itemKey}
          data={data}
          view={view}
          onViewChange={setView}
        />
        <div className="grid min-h-0 flex-1 grid-cols-[minmax(0,1fr)_17rem] overflow-auto max-[900px]:grid-cols-1">
          <AttachmentBody data={data} view={view} />
          <AttachmentSidePanel projectKey={project.key} itemKey={itemKey} data={data} />
        </div>
      </>
    );
  } else if (query.error) {
    const error = query.error;
    const notFound = error instanceof ApiRequestError && error.code === 'not_found';
    const tooLarge = error instanceof ApiRequestError && error.code === 'markdown_too_large';
    content = (
      <AttachmentUnavailable
        kind={notFound ? 'notFound' : 'loadFailed'}
        message={error.message}
        projectKey={project.key}
        itemKey={itemKey}
        downloadUrl={
          tooLarge ? `/api/attachments/${encodeURIComponent(attachmentId)}/content` : undefined
        }
      />
    );
  } else {
    content = null;
  }

  return (
    <section
      data-testid={ids.screen}
      aria-label="Attachment"
      className="flex min-h-0 flex-1 flex-col"
    >
      {content}
    </section>
  );
}
