import { Link } from 'react-router-dom';
import type { AttachmentDto } from '../../../api/attachments';
import { routePath } from '../../../routes';
import { formatSize } from '../../workItem/side/fileSize';
import { attachmentTestIds as ids } from '../testIds';
import { PanelHeading } from './PanelHeading';

// "Markdown on SHIP-n": this file and the item's other Markdown files, each opening on its own page.
export function SiblingsPanel({
  projectKey,
  itemKey,
  current,
  siblings,
}: {
  projectKey: string;
  itemKey: string;
  current: AttachmentDto;
  siblings: AttachmentDto[];
}) {
  return (
    <section
      data-testid={ids.siblings}
      aria-label={`Markdown on ${itemKey}`}
      className="grid gap-1"
    >
      <PanelHeading>Markdown on {itemKey}</PanelHeading>
      <ul className="m-0 grid list-none overflow-hidden rounded-md border border-edge bg-surface p-0">
        {[current, ...siblings].map((file) => (
          <li
            key={file.id}
            data-testid={ids.sibling}
            aria-current={file.id === current.id ? 'page' : undefined}
            className="flex min-h-7.5 items-center gap-1.5 border-t border-edge/60 px-2 text-[length:var(--text-sm)] first:border-t-0 aria-[current=page]:bg-signal-soft"
          >
            {file.id === current.id ? (
              <span className="min-w-0 flex-1 truncate">{file.name}</span>
            ) : (
              <Link
                to={routePath('workItemAttachment', {
                  key: projectKey,
                  itemKey,
                  attachmentId: file.id,
                })}
                className="min-w-0 flex-1 truncate text-ink no-underline hover:text-signal hover:underline"
              >
                {file.name}
              </Link>
            )}
            <span className="text-[length:var(--text-xs)] whitespace-nowrap text-ink-muted">
              {formatSize(file.sizeBytes)}
            </span>
          </li>
        ))}
      </ul>
    </section>
  );
}
