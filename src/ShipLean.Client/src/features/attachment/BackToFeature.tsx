import { Link } from 'react-router-dom';
import { routePath } from '../../routes';
import { attachmentTestIds as ids } from './testIds';

// The way back to the work item the file is attached to.
export function BackToFeature({
  projectKey,
  itemKey,
  title,
}: {
  projectKey: string;
  itemKey: string;
  title?: string;
}) {
  return (
    <Link
      data-testid={ids.back}
      to={routePath('workItem', { key: projectKey, itemKey })}
      className="inline-flex items-center gap-1.5 text-[length:var(--text-sm)] text-ink-muted no-underline hover:text-signal hover:underline"
    >
      <span aria-hidden="true">←</span>
      <span data-testid={ids.workItem}>
        {title === undefined ? itemKey : `${itemKey} ${title}`}
      </span>
    </Link>
  );
}
