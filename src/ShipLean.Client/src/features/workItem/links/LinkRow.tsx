import { Link } from 'react-router-dom';
import type { DraftLink } from '../form/draft';
import { LinkCardBadge } from './LinkCardBadge';
import { routePath } from '../../../routes';
import { workItemTestIds as ids } from '../testIds';
import { isWebUrl, linkTypeLabels } from './linkTypes';

const rowButton =
  'size-6 rounded-control text-ink-muted hover:bg-signal-soft focus-visible:outline-2 focus-visible:outline-signal';

// One link: its type, its target, the pull or merge request state a webhook reported (with "auto" when the webhook
// added the link) and, for a link not saved yet, a note that it is waiting for Save.
export function LinkRow({
  link,
  projectKey,
  onEdit,
  onRemove,
}: {
  link: DraftLink;
  projectKey: string;
  onEdit: () => void;
  onRemove: () => void;
}) {
  const target =
    link.type === 'url' ? (
      link.url !== null && isWebUrl(link.url) ? (
        <a
          href={link.url}
          target="_blank"
          rel="noopener noreferrer"
          title={link.url}
          className="truncate text-signal hover:underline"
        >
          {link.title || link.url}
        </a>
      ) : (
        <span className="truncate">{link.title || link.url}</span>
      )
    ) : (
      <>
        <Link
          to={routePath('workItem', { key: projectKey, itemKey: link.key ?? '' })}
          className="font-semibold text-signal hover:underline"
        >
          {link.key}
        </Link>
        <span className="truncate" title={link.title ?? undefined}>
          {link.title}
        </span>
      </>
    );
  return (
    <li
      data-testid={ids.linkRow}
      data-link-type={link.type}
      className="flex items-center gap-2 text-[length:var(--text-sm)]"
    >
      <span className="w-20 shrink-0 text-[length:var(--text-xs)] text-ink-muted">
        {linkTypeLabels[link.type]}
      </span>
      {target}
      {link.card ? <LinkCardBadge card={link.card} autoLinked={link.autoLinked} /> : null}
      {link.id === null ? (
        <span className="shrink-0 text-[length:var(--text-xs)] text-caution">unsaved</span>
      ) : null}
      <span className="ml-auto flex shrink-0">
        <button
          type="button"
          data-testid={ids.linkEdit}
          aria-label="Edit link"
          className={rowButton}
          onClick={onEdit}
        >
          ✎
        </button>
        <button
          type="button"
          data-testid={ids.linkRemove}
          aria-label="Remove link"
          className={rowButton}
          onClick={onRemove}
        >
          ✕
        </button>
      </span>
    </li>
  );
}
