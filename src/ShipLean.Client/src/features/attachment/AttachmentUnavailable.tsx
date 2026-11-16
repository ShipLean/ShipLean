import type { AttachmentDto } from '@shiplean/core/api/attachments';
import { useTouchShell } from '../../touch/useTouchShell';
import { formatSize } from '../workItem/side/fileSize';
import { BackToFeature } from './BackToFeature';
import { attachmentMeta } from './header/attachmentMeta';
import { attachmentTestIds as ids } from './testIds';
import { FileBadge } from './touch/FileBadge';

// The file can't be shown: it isn't there (not found) or the server refused to render it (too big, not Markdown, down).
// A file that is only too big to render can still be downloaded; on a phone that page keeps the file's header, says so
// in a heading and offers a primary Download with the size (`file` is the item's attachments-list entry).
export function AttachmentUnavailable({
  kind,
  message,
  projectKey,
  itemKey,
  downloadUrl,
  file,
}: {
  kind: 'notFound' | 'loadFailed';
  message: string;
  projectKey: string;
  itemKey: string;
  downloadUrl?: string;
  file?: AttachmentDto;
}) {
  const { touch } = useTouchShell();
  const tooLargeOnPhone = touch && downloadUrl !== undefined;
  return (
    <div
      data-testid={kind === 'notFound' ? ids.notFound : ids.loadFailed}
      role={kind === 'notFound' ? undefined : 'alert'}
      className="grid content-start justify-items-start gap-2 p-4"
    >
      {tooLargeOnPhone && file ? (
        <header
          data-testid={ids.header}
          className="-mx-4 -mt-4 mb-2 grid w-[calc(100%+2rem)] gap-1 border-b border-edge px-3 pt-2 pb-3"
        >
          <h1 className="m-0 flex min-h-[var(--touch-target)] min-w-0 items-center gap-2 font-[family-name:var(--font-mono)] text-[length:var(--text-lg)] font-semibold">
            <FileBadge />
            <span data-testid={ids.name} className="min-w-0 truncate">
              {file.name}
            </span>
          </h1>
          <span data-testid={ids.meta} className="text-[length:var(--text-sm)] text-ink-muted">
            {attachmentMeta(file)}
          </span>
        </header>
      ) : null}
      {tooLargeOnPhone ? <FileBadge large /> : null}
      {tooLargeOnPhone ? (
        <h2
          data-testid={ids.tooLargeHeading}
          className="m-0 text-[length:var(--text-xl)] font-semibold"
        >
          Too large to show here
        </h2>
      ) : null}
      <BackToFeature projectKey={projectKey} itemKey={itemKey} />
      <p className="m-0 text-[length:var(--text-md)]">{message}</p>
      {downloadUrl === undefined ? null : (
        <a
          data-testid={ids.loadFailedDownload}
          href={downloadUrl}
          download
          className={
            tooLargeOnPhone
              ? 'btn-signal touch:h-[var(--touch-target)] w-full no-underline'
              : 'inline-flex h-7 touch:h-[var(--touch-target)] touch:w-full touch:justify-center items-center rounded-control border border-edge bg-panel px-3 text-[length:var(--text-sm)] text-ink no-underline'
          }
        >
          {tooLargeOnPhone && file ? `Download (${formatSize(file.sizeBytes)})` : 'Download'}
        </a>
      )}
    </div>
  );
}
