import { useState } from 'react';
import { ApiRequestError } from '../../api/client';
import type { AttachmentSlotProps } from '../../slots/AttachmentSlot';
import { useAttachmentMarkdown } from '../feature/featureApi';
import { AttachmentUnavailable } from './AttachmentUnavailable';
import { AttachmentBody } from './body/AttachmentBody';
import { AttachmentEditor } from './body/AttachmentEditor';
import { AttachmentHeader } from './header/AttachmentHeader';
import type { AttachmentView } from './header/ViewToggle';
import { useReplaceAttachment } from './header/useReplaceAttachment';
import { AttachmentSidePanel } from './side/AttachmentSidePanel';
import { attachmentTestIds as ids } from './testIds';

// One Markdown attachment on its own page (attachment.html): header and toolbar, the body rendered, as source or in the
// editor, and the side panel. One request fills it; a teammate's change reads it again, and a replace or a save moves
// to the new copy. The draft outlives switching views; Cancel or Save drops it.
export function AttachmentScreen({ project, itemKey, attachmentId }: AttachmentSlotProps) {
  const [view, setView] = useState<AttachmentView>('rendered');
  const [draft, setDraft] = useState<string | null>(null);
  const query = useAttachmentMarkdown(project.id, attachmentId);
  const data = query.data;
  const upload = useReplaceAttachment(
    project.id,
    project.key,
    itemKey,
    attachmentId,
    data?.workItem.id ?? '',
  );
  const saving = upload.isPending && upload.variables.savedText === true;
  const uploadError = upload.error?.message ?? null;
  const savedText = upload.variables?.savedText === true;

  let content;
  if (data) {
    content = (
      <>
        <AttachmentHeader
          projectKey={project.key}
          itemKey={itemKey}
          data={data}
          view={view}
          onViewChange={setView}
          replacing={upload.isPending}
          replaceError={savedText ? null : uploadError}
          onReplace={(file) => upload.mutate({ file, currentName: data.attachment.name })}
        />
        <div className="grid min-h-0 flex-1 grid-cols-[minmax(0,1fr)_17rem] overflow-auto max-[900px]:grid-cols-1">
          {view === 'edit' ? (
            <AttachmentEditor
              projectId={project.id}
              workItemId={data.workItem.id}
              itemKey={itemKey}
              name={data.attachment.name}
              value={draft ?? data.markdown}
              changed={draft !== null && draft !== data.markdown}
              saving={saving}
              error={savedText ? uploadError : null}
              onChange={setDraft}
              onSave={() => {
                const file = new File([draft ?? data.markdown], data.attachment.name, {
                  type: 'text/markdown',
                });
                upload.mutate(
                  { file, currentName: data.attachment.name, savedText: true },
                  {
                    onSuccess: () => {
                      setDraft(null);
                      setView('rendered');
                    },
                  },
                );
              }}
              onCancel={() => {
                setDraft(null);
                setView('rendered');
              }}
            />
          ) : (
            <AttachmentBody data={data} view={view} />
          )}
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
