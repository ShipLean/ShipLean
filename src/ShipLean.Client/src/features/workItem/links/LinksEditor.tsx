import { Fragment, useState } from 'react';
import { LINK_TYPES, type LinkType } from '@shiplean/core/api/persisted';
import type { ProjectDto } from '@shiplean/core/api/projects';
import { useDraftLinks } from '../form/DraftContext';
import type { AddLinkType } from '../../../routes';
import { workItemTestIds as ids } from '../testIds';
import { ItemLink } from '../../itemPeek/ItemLink';
import { useWorkItem } from '../workItemApi';
import { LinkForm } from './LinkForm';
import { LinkRow } from './LinkRow';
import { linkTypeLabels } from './linkTypes';

// The links of the work item as the form's draft holds them: added, edited and removed here, written by the form's Save.
// A Bug can add a Caused by link to the Task that introduced it (+ Caused by, or ?addLink=causedBy opens the picker preset);
// a Task lists the Bugs that point at it as read-only Caused rows.
export function LinksEditor({
  project,
  itemKey,
  addLink,
}: {
  project: ProjectDto;
  itemKey: string;
  addLink?: AddLinkType;
}) {
  const { links, changed, setLinks } = useDraftLinks();
  const item = useWorkItem(project.id, itemKey).data;
  const isBug = item?.type.id === 'bug';
  const caused = item?.caused ?? [];
  // null = closed, 'new' = adding, otherwise the uid of the link being edited.
  const [picked, setPicked] = useState<string | null>(null);
  const [preset, setPreset] = useState<LinkType | undefined>(undefined);
  // ?addLink=causedBy on a Bug opens the picker once, until it is closed or another opens.
  const [presetUsed, setPresetUsed] = useState(false);
  const fromAddress = isBug && addLink === 'causedBy' && !presetUsed && picked === null;
  const open = fromAddress ? 'new' : picked;
  const presetType = fromAddress ? 'causedBy' : preset;
  const setOpen = (next: string | null) => {
    setPresetUsed(true);
    setPicked(next);
  };
  // Grouped by type in the order of LINK_TYPES; within a type, in the order the draft holds them.
  const grouped = LINK_TYPES.flatMap((type) => links.filter((link) => link.type === type));
  const editing = links.find((link) => link.uid === open) ?? null;
  return (
    <section
      data-testid={ids.links}
      aria-label="Links"
      className="grid grid-cols-[minmax(0,1fr)] content-start gap-2"
    >
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
          {links.length + caused.length}
        </span>
        <button
          type="button"
          data-testid={ids.linkAdd}
          className="ml-auto h-6 rounded-control px-2 text-[length:var(--text-xs)] text-signal hover:bg-signal-soft touch:h-[var(--touch-target)]"
          onClick={() => {
            setPreset(undefined);
            setOpen(open === 'new' ? null : 'new');
          }}
        >
          + Add link
        </button>
        {isBug ? (
          <button
            type="button"
            data-testid={ids.linkAddCausedBy}
            className="h-6 rounded-control px-2 text-[length:var(--text-xs)] text-signal hover:bg-signal-soft touch:h-[var(--touch-target)]"
            onClick={() => {
              setPreset('causedBy');
              setOpen('new');
            }}
          >
            + Caused by
          </button>
        ) : null}
      </div>
      {open !== null ? (
        <LinkForm
          key={`${open}-${presetType ?? ''}`}
          itemType={item?.type.id}
          presetType={presetType}
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
      {links.length === 0 && caused.length === 0 ? (
        <span data-testid={ids.linksEmpty} className="text-[length:var(--text-sm)] text-ink-muted">
          No links.
        </span>
      ) : (
        <ul className="grid grid-cols-[minmax(0,1fr)] gap-1">
          {caused.map((bug) => (
            <li
              key={bug.id}
              data-testid={ids.linkCausedByRow}
              data-link-type="causedBy"
              className="flex items-center gap-2 text-[length:var(--text-sm)] touch:min-h-[var(--touch-target)]"
            >
              <span className="w-20 shrink-0 text-[length:var(--text-xs)] text-ink-muted touch:w-16">
                Caused
              </span>
              <ItemLink
                projectKey={project.key}
                itemKey={bug.key}
                className="font-semibold text-signal hover:underline touch:inline-flex touch:min-h-[var(--touch-target)] touch:items-center"
              >
                {bug.key}
              </ItemLink>
              <span
                className="truncate touch:min-w-0 touch:overflow-visible touch:whitespace-normal"
                title={bug.title}
              >
                {bug.title}
              </span>
            </li>
          ))}
          {grouped.map((link, index) => (
            <Fragment key={link.uid}>
              {link.type !== grouped[index - 1]?.type ? (
                <li
                  aria-hidden="true"
                  className="hidden px-1 pt-1 text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase touch:block"
                >
                  {linkTypeLabels[link.type]}
                </li>
              ) : null}
              <LinkRow
                link={link}
                projectKey={project.key}
                onEdit={() => setOpen(link.uid)}
                onRemove={() => setLinks(links.filter((existing) => existing.uid !== link.uid))}
              />
            </Fragment>
          ))}
        </ul>
      )}
    </section>
  );
}
