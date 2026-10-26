import { FieldCell } from '../../workItem/form/FieldCell';
import { featureTestIds as ids } from '../testIds';

// The Feature's slug: set at creation, read-only after, with a button that copies it.
export function SlugField({ slug }: { slug: string | null }) {
  return (
    <FieldCell label="Slug" field="slug" changed={false}>
      <span className="inline-flex h-7 items-center gap-2 text-[length:var(--text-sm)]">
        <code data-testid={ids.stripSlug} className="font-[family-name:var(--font-mono)]">
          {slug ?? 'none'}
        </code>
        {slug ? (
          <button
            type="button"
            aria-label="Copy slug"
            className="rounded-control px-1 text-[length:var(--text-xs)] text-signal hover:bg-signal-soft"
            onClick={() => void navigator.clipboard.writeText(slug)}
          >
            Copy
          </button>
        ) : null}
      </span>
    </FieldCell>
  );
}
