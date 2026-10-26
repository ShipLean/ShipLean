import { useState } from 'react';
import { LINK_TYPES } from '../../../api/persisted';
import type { ProjectDto } from '../../../api/projects';
import { useDraftLinks } from '../form/DraftContext';
import { workItemTestIds as ids } from '../testIds';
import { LinkForm } from './LinkForm';
import { LinkRow } from './LinkRow';

// The links of the work item as the form's draft holds them: added, edited and removed here, written by the form's Save.
export function LinksEditor({ project, itemKey }: { project: ProjectDto; itemKey: string }) {
  const { links, changed, setLinks } = useDraftLinks();
  // null = closed, 'new' = adding, otherwise the uid of the link being edited.
  const [open, setOpen] = useState<string | null>(null);
  // Grouped by type in the order of LINK_TYPES; within a type, in the order the draft holds them.
  const grouped = LINK_TYPES.flatMap((type) => links.filter((link) => link.type === type));
  const editing = links.find((link) => link.uid === open) ?? null;
  return (
    <section data-testid={ids.links} aria-label="Links" className="grid content-start gap-2">
      <div className="flex items-center gap-2">
        <span className="flex items-center gap-1 text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase">
          Links
          {changed ? (
            <span
              data-testid={ids.changedDot}
              role="img"
              aria-label="Unsaved change"
              className="size-1.5 rounded-full bg-signal"
            />
          ) : null}
        </span>
        <span data-testid={ids.linksCount} className="text-[length:var(--text-xs)] text-ink-muted">
          {links.length}
        </span>
        <button
          type="button"
          data-testid={ids.linkAdd}
          className="ml-auto h-6 rounded-control px-2 text-[length:var(--text-xs)] text-signal hover:bg-signal-soft"
          onClick={() => setOpen(open === 'new' ? null : 'new')}
        >
          + Add link
        </button>
      </div>
      {open !== null ? (
        <LinkForm
          key={open}
          projectId={project.id}
          itemKey={itemKey}
          others={links.filter((link) => link.uid !== open)}
          initial={editing}
          onCancel={() => setOpen(null)}
          onSubmit={(link) => {
            setLinks(
              editing
                ? links.map((existing) => (existing.uid === editing.uid ? link : existing))
                : [...links, link],
            );
            setOpen(null);
          }}
        />
      ) : null}
      {links.length === 0 ? (
        <span data-testid={ids.linksEmpty} className="text-[length:var(--text-sm)] text-ink-muted">
          No links.
        </span>
      ) : (
        <ul className="grid gap-1">
          {grouped.map((link) => (
            <LinkRow
              key={link.uid}
              link={link}
              projectKey={project.key}
              onEdit={() => setOpen(link.uid)}
              onRemove={() => setLinks(links.filter((existing) => existing.uid !== link.uid))}
            />
          ))}
        </ul>
      )}
    </section>
  );
}
