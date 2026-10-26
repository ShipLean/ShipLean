import { BackToFeature } from './BackToFeature';
import { attachmentTestIds as ids } from './testIds';

// The file can't be shown: it isn't there (not found) or the server refused to render it (too big, not Markdown, down).
// A file that is only too big to render can still be downloaded.
export function AttachmentUnavailable({
  kind,
  message,
  projectKey,
  itemKey,
  downloadUrl,
}: {
  kind: 'notFound' | 'loadFailed';
  message: string;
  projectKey: string;
  itemKey: string;
  downloadUrl?: string;
}) {
  return (
    <div
      data-testid={kind === 'notFound' ? ids.notFound : ids.loadFailed}
      role={kind === 'notFound' ? undefined : 'alert'}
      className="grid content-start justify-items-start gap-2 p-4"
    >
      <BackToFeature projectKey={projectKey} itemKey={itemKey} />
      <p className="m-0 text-[length:var(--text-md)]">{message}</p>
      {downloadUrl === undefined ? null : (
        <a
          data-testid={ids.loadFailedDownload}
          href={downloadUrl}
          download
          className="inline-flex h-7 items-center rounded-control border border-edge bg-panel px-3 text-[length:var(--text-sm)] text-ink no-underline"
        >
          Download
        </a>
      )}
    </div>
  );
}
