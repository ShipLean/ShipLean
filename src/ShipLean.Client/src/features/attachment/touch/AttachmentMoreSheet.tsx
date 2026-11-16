import { useRef } from 'react';
import type { AttachmentDto } from '@shiplean/core/api/attachments';
import { showToast } from '../../../components/toastStore';
import { MoreButton } from '../../../touch/MoreButton';
import { formatSize } from '../../workItem/side/fileSize';
import { attachmentTestIds as ids } from '../testIds';
import { downloadIcon, infoIcon, linkIcon, uploadIcon } from './MenuIcons';

// The ⋯ on a phone (D2): Download (with the size), Replace…, Copy link, then About this file after a divider, in one action sheet. Replace… opens the file
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
        variant="bordered"
        actions={[
          {
            id: 'download',
            label: 'Download',
            icon: downloadIcon,
            hint: formatSize(attachment.sizeBytes),
            group: '',
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
            icon: uploadIcon,
            hint: 'Pick a file; the old copy is soft-deleted',
            group: '',
            onSelect: () => picker.current?.click(),
          },
          {
            id: 'copy-link',
            label: 'Copy link',
            icon: linkIcon,
            group: '',
            onSelect: () =>
              void navigator.clipboard
                .writeText(window.location.href)
                .then(() => showToast('Copied link', { tone: 'ok' })),
          },
          { id: 'about', label: 'About this file', icon: infoIcon, onSelect: onAbout },
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
