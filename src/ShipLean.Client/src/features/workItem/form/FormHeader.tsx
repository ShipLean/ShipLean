import { Link } from 'react-router-dom';
import type { WorkItemDto } from '../../../api/workItems';
import { TypeIcon } from '../../../components/TypeIcon';
import { routePath } from '../../../routes';
import { workItemTestIds as ids } from '../testIds';
import { fieldLabels, type DraftField } from './draft';
import { inputClass } from './inputClasses';

const buttonClass =
  'inline-flex h-7 items-center rounded-control border px-3 text-[length:var(--text-sm)] font-medium focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-signal disabled:cursor-default disabled:opacity-50';

// Back link, type and key, the title field, the save state, Discard and Save.
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
  const who = (name: string | undefined) => name ?? 'Jira import';
  return (
    <section
      aria-label="Work item header"
      className="grid gap-2 border-b border-edge bg-panel px-4 py-3"
    >
      <div className="flex items-center gap-2 text-[length:var(--text-sm)]">
        <Link
          data-testid={ids.back}
          to={routePath('storiesBacklog', { key: projectKey })}
          className="text-signal hover:underline"
        >
          ← Back to Stories backlog
        </Link>
        <span
          data-testid={ids.meta}
          className="ml-auto text-[length:var(--text-xs)] text-ink-muted"
        >
          Created by {who(item.createdBy?.name)}, {item.createdAtLabel} · Changed by{' '}
          {who(item.changedBy?.name)}, {item.changedAtLabel} · revision {item.revision}
        </span>
      </div>
      <div className="flex items-center gap-3">
        <span
          data-testid={ids.ident}
          className="flex items-center gap-2 text-[length:var(--text-sm)]"
        >
          <TypeIcon type={item.type.id} />
          <span className="text-ink-muted">{item.type.name}</span>
          <span data-testid={ids.key} className="font-semibold">
            {item.key}
          </span>
        </span>
        <input
          data-testid={ids.title}
          aria-label="Title"
          autoComplete="off"
          className={`${inputClass} h-8 flex-1 text-[length:var(--text-md)] font-semibold`}
          value={title}
          onChange={(event) => onTitleChange(event.target.value)}
        />
        <span
          data-testid={ids.saveState}
          role="status"
          title={
            dirty
              ? `Unsaved changes to ${changed.map((f) => fieldLabels[f]).join(', ')}`
              : undefined
          }
          className={`text-[length:var(--text-xs)] ${dirty ? 'text-caution' : 'text-ink-muted'}`}
        >
          {dirty
            ? `Unsaved: ${changed.map((f) => fieldLabels[f]).join(', ')}`
            : `Saved · revision ${item.revision}`}
        </span>
        <button
          type="button"
          data-testid={ids.discard}
          hidden={!dirty}
          className={`${buttonClass} border-edge bg-panel text-ink hover:bg-signal-soft`}
          onClick={onDiscard}
        >
          Discard
        </button>
        <button
          type="button"
          data-testid={ids.save}
          title="Save (Ctrl+S)"
          disabled={!dirty || saving}
          className={`${buttonClass} border-signal bg-signal text-signal-ink`}
          onClick={onSave}
        >
          Save
        </button>
      </div>
    </section>
  );
}
