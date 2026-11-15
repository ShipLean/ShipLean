import { Fragment, useId, useState, type KeyboardEvent } from 'react';
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom';
import { matchRoute, routePath } from '../../../routes';
import { sameScreenIn } from '../sameScreenIn';
import { shellTestIds as ids } from '../testIds';
import { useMe, useProjects, useRecentProjectIds } from '../useShellQueries';
import { PickerOption } from './PickerOption';
import { pickerGroups, type PickerGroup } from './pickerList';

// The searchable list of the projects you are in. Picking one opens the same screen there.
export function ProjectPicker({
  currentKey,
  onClose,
}: {
  currentKey: string | undefined;
  onClose: () => void;
}) {
  const picker = useProjectPicker(onClose);

  function onDialogKeyDown(event: KeyboardEvent) {
    if (event.key === 'Escape') {
      event.preventDefault();
      event.stopPropagation();
      onClose();
    }
  }

  return (
    <div
      role="dialog"
      aria-label="Switch project"
      data-testid={ids.picker}
      onKeyDown={onDialogKeyDown}
      className="absolute top-[72px] left-2 z-50 flex w-[300px] flex-col overflow-hidden rounded-panel border border-edge bg-panel shadow-[0_12px_32px_rgb(0_0_0/0.18)]"
    >
      <div className="border-b border-edge p-2">
        <input
          autoFocus
          {...picker.searchProps}
          className="h-7 w-full rounded-control border border-edge bg-canvas px-2 text-[length:var(--text-sm)]"
        />
      </div>
      <PickerList picker={picker} currentKey={currentKey} className="max-h-80 overflow-auto p-1" />
      {picker.canCreate ? (
        <footer className="flex gap-1 border-t border-edge bg-canvas px-2 py-1 text-[length:var(--text-sm)]">
          <PickerFooterLinks
            onClose={onClose}
            className="rounded-control px-2 py-1 hover:bg-signal-soft"
          />
        </footer>
      ) : null}
    </div>
  );
}

export type ProjectPickerState = ReturnType<typeof useProjectPicker>;

// The picker's state shared by the desktop popover and the touch sheet: the search, the highlighted option, the groups
// and the pick. Picking the project of the route you are on changes nothing; on an install-level page (where the
// side menu kept a project) it opens that project.
export function useProjectPicker(onClose: () => void) {
  const navigate = useNavigate();
  const routeKey = useParams().key;
  const listId = useId();
  const { pathname, search: locationSearch } = useLocation();
  const projects = useProjects().data ?? [];
  const recent = useRecentProjectIds().data ?? [];
  const canCreate = useMe().data?.canCreateProjects === true;
  const [search, setSearch] = useState('');
  const [highlight, setHighlight] = useState(0);

  const groups: PickerGroup[] = pickerGroups(projects, recent, search);
  const shown = groups.flatMap((group) => group.projects);
  const at = Math.min(highlight, Math.max(shown.length - 1, 0));

  function pick(key: string) {
    onClose();
    if (key !== routeKey) {
      navigate(sameScreenIn(matchRoute(pathname, locationSearch), key));
    }
  }

  function onKeyDown(event: KeyboardEvent) {
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      const step = event.key === 'ArrowDown' ? 1 : -1;
      setHighlight((at + step + shown.length) % Math.max(shown.length, 1));
    } else if (event.key === 'Enter' && shown[at]) {
      event.preventDefault();
      pick(shown[at].key);
    }
  }

  const optionId = (projectId: string) => `${listId}-${projectId}`;
  const active = shown[at];

  return {
    listId,
    search,
    groups,
    shown,
    at,
    canCreate,
    pick,
    optionId,
    searchProps: {
      type: 'search',
      role: 'combobox',
      'aria-expanded': true,
      'aria-controls': listId,
      'aria-autocomplete': 'list',
      'aria-activedescendant': active ? optionId(active.id) : undefined,
      'data-testid': ids.pickerSearch,
      'aria-label': 'Search projects',
      placeholder: 'Search projects by name or key',
      value: search,
      onChange: (event: { target: { value: string } }) => {
        setSearch(event.target.value);
        setHighlight(0);
      },
      onKeyDown,
    } as const,
  };
}

// The listbox: Recent and All your projects, or the matches while searching.
export function PickerList({
  picker,
  currentKey,
  className,
}: {
  picker: ProjectPickerState;
  currentKey: string | undefined;
  className: string;
}) {
  const { listId, search, groups, shown, at, pick, optionId } = picker;
  return (
    <ul id={listId} role="listbox" aria-label="Projects" className={`m-0 list-none ${className}`}>
      {shown.length === 0 ? (
        <li
          data-testid={ids.pickerNone}
          className="p-3 text-center text-[length:var(--text-sm)] text-ink-muted"
        >
          {search.trim() === '' ? 'No projects yet' : `No project matches “${search}”`}
        </li>
      ) : null}
      {groups.map((group) => (
        <Fragment key={group.title ?? 'matches'}>
          {group.title ? (
            <li
              role="presentation"
              data-testid={ids.pickerGroup}
              className="px-2 pt-1.5 pb-0.5 text-[length:var(--text-2xs)] font-semibold tracking-[0.05em] text-ink-muted uppercase"
            >
              {group.title}
            </li>
          ) : null}
          {group.projects.map((project) => (
            <PickerOption
              key={project.id}
              id={optionId(project.id)}
              project={project}
              search={search}
              current={project.key === currentKey}
              highlighted={shown.indexOf(project) === at}
              onPick={() => pick(project.key)}
            />
          ))}
        </Fragment>
      ))}
    </ul>
  );
}

// + New project and Import from Jira, for callers who may create projects.
export function PickerFooterLinks({
  onClose,
  className,
}: {
  onClose: () => void;
  className: string;
}) {
  return (
    <>
      <Link
        to={routePath('newProject', {})}
        data-testid={ids.pickerNewProject}
        className={className}
        onClick={onClose}
      >
        + New project
      </Link>
      <Link
        to={routePath('importStart', {})}
        data-testid={ids.pickerImport}
        className={className}
        onClick={onClose}
      >
        Import from Jira
      </Link>
    </>
  );
}
