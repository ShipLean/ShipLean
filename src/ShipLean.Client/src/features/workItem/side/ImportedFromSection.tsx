import type { OriginalSystemDto } from '../../../api/workItems';
import { workItemTestIds as ids } from '../testIds';

// Where an imported item came from: the system, the item's id there, a link to it when the import kept one and that link's host.
export function ImportedFromSection({
  original,
  itemKey,
}: {
  original: OriginalSystemDto;
  itemKey: string;
}) {
  const system = original.system.charAt(0).toUpperCase() + original.system.slice(1);
  const host = hostOf(original.url);
  return (
    <section
      data-testid={ids.importedFrom}
      aria-label={`Imported from ${system}`}
      className="grid content-start gap-2"
    >
      <span className="text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase">
        Imported from {system}
      </span>
      <div className="flex items-center gap-2">
        {original.url !== null ? (
          <a
            data-testid={ids.importedFromLink}
            href={original.url}
            target="_blank"
            rel="noopener noreferrer"
            className="text-[length:var(--text-sm)] text-signal hover:underline"
          >
            {original.id} ↗
          </a>
        ) : (
          <span data-testid={ids.importedFromLink} className="text-[length:var(--text-sm)]">
            {original.id}
          </span>
        )}
        {host !== null ? (
          <span
            data-testid={ids.importedFromHost}
            className="ml-auto text-[length:var(--text-xs)] text-ink-muted"
          >
            {host}
          </span>
        ) : null}
      </div>
      <p
        data-testid={ids.importedFromHint}
        className="m-0 text-[length:var(--text-xs)] text-ink-muted"
      >
        Re-running the import matches on this reference and updates {itemKey} instead of making a
        copy.
      </p>
    </section>
  );
}

// The link's host, shown beside it; null when the import kept no link or a link that is not a URL.
function hostOf(url: string | null): string | null {
  if (url === null) return null;
  try {
    return new URL(url).host;
  } catch {
    return null;
  }
}
