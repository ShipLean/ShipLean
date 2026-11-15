import { useRef } from 'react';
import type { AttachmentDto } from '@shiplean/core/api/attachments';
import { showToast } from '../../../components/toastStore';
import { MoreButton } from '../../../touch/MoreButton';
import { attachmentTestIds as ids } from '../testIds';

// The ⋯ on a phone (D2): Download, Replace…, Copy link and About this file in one action sheet. Replace… opens the file
// picker, which lives here so the sheet can close first; the file goes up as soon as it is chosen.
export function AttachmentMoreSheet({
  attachment,
  replacing,
  onReplace,
  onAbout,
}: {
  attachment: AttachmentDto;
  replacing: boolean;
  onReplace: (file: File) => void;
  onAbout: () => void;
}) {
  const picker = useRef<HTMLInputElement>(null);
  return (
    <>
      <MoreButton
        label="Attachment actions"
        actions={[
          {
            id: 'download',
            label: 'Download',
            onSelect: () => {
              const link = document.createElement('a');
              link.href = attachment.downloadUrl;
              link.download = attachment.name;
              link.click();
            },
          },
          {
            id: 'replace',
            label: replacing ? 'Replacing…' : 'Replace…',
            disabled: replacing,
            onSelect: () => picker.current?.click(),
          },
          {
            id: 'copy-link',
            label: 'Copy link',
            onSelect: () =>
              void navigator.clipboard
                .writeText(window.location.href)
                .then(() => showToast('Copied link', { tone: 'ok' })),
          },
          { id: 'about', label: 'About this file', onSelect: onAbout },
        ]}
      />
      <input
        ref={picker}
        data-testid={ids.replaceInput}
        type="file"
        accept=".md,text/markdown"
        aria-label="Replace file"
        className="hidden"
        onChange={(event) => {
          const file = event.target.files?.[0];
          event.target.value = '';
          if (file) onReplace(file);
        }}
      />
    </>
  );
}
