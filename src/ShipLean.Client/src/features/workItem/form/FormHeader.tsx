import { Link } from 'react-router-dom';
import type { WorkItemDto } from '../../../api/workItems';
import { TypeIcon } from '../../../components/TypeIcon';
import { showToast } from '../../../components/toastStore';
import { routePath } from '../../../routes';
import { useInItemPeek } from '../../itemPeek/ItemPeekContext';
import { useTouchShell } from '../../../touch/useTouchShell';
import { workItemTestIds as ids } from '../testIds';
import { changedFieldNames } from '../touch/changedFieldNames';
import type { DraftField } from './draft';
import { inputClass } from './inputClasses';

const buttonClass =
  'inline-flex h-7 items-center rounded-control border px-3 text-[length:var(--text-sm)] font-medium focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-signal disabled:cursor-default disabled:opacity-50';

// Back link, type and key, the title field, the save state, Discard and Save. At 900px and below (SHIP-714) the type and
// key (with copy) sit over a wrapping title that Enter finishes, the created/changed line goes under it, and the back
// link, save state, Discard and Save give way to the top bar's ‹ and the Save bar.
export function FormHeader({
  item,
  projectKey,
  title,
  changed,
  saving,
  onTitleChange,
  onSave,
  onDiscard,
}: {
  item: WorkItemDto;
  projectKey: string;
  title: string;
  changed: readonly DraftField[];
  saving: boolean;
  onTitleChange: (title: string) => void;
  onSave: () => void;
  onDiscard: () => void;
}) {
  const dirty = changed.length > 0;
  // The modal has its own close, so it has no way back.
  const inModal = useInItemPeek();
  const who = (name: string | undefined) => name ?? 'Jira import';
  const { touch } = useTouchShell();
  const names = changedFieldNames(changed);
  return (
    <section
      aria-label="Work item header"
      className="grid gap-2 border-b border-edge bg-panel px-4 py-3"
    >
      <div className="flex items-center gap-2 text-[length:var(--text-sm)] touch:order-2">
        {inModal ? null : (
          <Link
            data-testid={ids.back}
            to={routePath('storiesBacklog', { key: projectKey })}
            className="text-signal hover:underline touch:hidden"
          >
            ← Back to Stories backlog
          </Link>
        )}
        <span
          data-testid={ids.meta}
          className="ml-auto min-w-0 text-right text-[length:var(--text-xs)] text-ink-muted [overflow-wrap:anywhere] touch:ml-0 touch:text-left"
        >
          Created by {who(item.createdBy?.name)}, {item.createdAtLabel} · Changed by{' '}
          {who(item.changedBy?.name)}, {item.changedAtLabel} · revision {item.revision}
        </span>
      </div>
      <div className="flex items-center gap-3 touch:flex-wrap touch:gap-x-2 touch:gap-y-0.5">
        <span
          data-testid={ids.ident}
          className="flex items-center gap-2 text-[length:var(--text-sm)] touch:basis-full"
        >
          <TypeIcon type={item.type.id} />
          <span className="text-ink-muted">{item.type.name}</span>
          <span data-testid={ids.key} className="font-semibold">
            {item.key}
          </span>
          {touch ? (
            <button
              type="button"
              data-testid={ids.copyKey}
              aria-label={`Copy ${item.key}`}
              className="inline-flex size-[var(--touch-target)] items-center justify-center rounded-control text-ink-muted"
              onClick={() =>
                void navigator.clipboard
                  .writeText(item.key)
                  .then(() => showToast(`Copied ${item.key}`, { tone: 'ok' }))
              }
            >
              <CopyIcon />
            </button>
          ) : null}
        </span>
        {touch ? (
          <textarea
            data-testid={ids.title}
            aria-label="Title"
            autoComplete="off"
            rows={1}
            enterKeyHint="done"
            // 19px over the 16px touch input rule (index.css): bigger, so iOS doesn't zoom either.
            className="w-full resize-none overflow-hidden rounded-control border border-transparent bg-transparent px-1.5 py-1 text-[19px]! leading-[1.3] font-semibold text-ink [field-sizing:content] focus-visible:bg-panel focus-visible:outline-2 focus-visible:outline-signal"
            value={title}
            onChange={(event) => onTitleChange(event.target.value.replace(/\s*\n\s*/g, ' '))}
            onKeyDown={(event) => {
              if (event.key !== 'Enter') return;
              event.preventDefault();
              event.currentTarget.blur();
            }}
          />
        ) : (
          <input
            data-testid={ids.title}
            aria-label="Title"
            autoComplete="off"
            className={`${inputClass} h-8 flex-1 text-[length:var(--text-md)] font-semibold`}
            value={title}
            onChange={(event) => onTitleChange(event.target.value)}
          />
        )}
        <span
          data-testid={ids.saveState}
          role="status"
          title={dirty ? `Unsaved changes to ${names}` : undefined}
          className={`text-[length:var(--text-xs)] ${dirty ? 'text-caution' : 'text-ink-muted'} touch:hidden`}
        >
          {dirty ? `Unsaved: ${names}` : `Saved · revision ${item.revision}`}
        </span>
        <button
          type="button"
          data-testid={ids.discard}
          hidden={!dirty}
          className={`${buttonClass} border-edge bg-panel text-ink hover:bg-signal-soft touch:hidden`}
          onClick={onDiscard}
        >
          Discard
        </button>
        <button
          type="button"
          data-testid={ids.save}
          title="Save (Ctrl+S)"
          disabled={!dirty || saving}
          className={`${buttonClass} border-signal bg-signal text-signal-ink touch:hidden`}
          onClick={onSave}
        >
          Save
        </button>
      </div>
    </section>
  );
}

// Two overlapping sheets: an icon rather than the ⧉ glyph, which many phone fonts lack (design/screens/item-touch.css).
function CopyIcon() {
  return (
    <svg
      aria-hidden="true"
      viewBox="0 0 16 16"
      width="15"
      height="15"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.5"
    >
      <rect x="5.5" y="5.5" width="8" height="8" rx="1.5" />
      <path d="M10.5 3.5v-.5A1.5 1.5 0 0 0 9 1.5H3A1.5 1.5 0 0 0 1.5 3v6A1.5 1.5 0 0 0 3 10.5h.5" />
    </svg>
  );
}
