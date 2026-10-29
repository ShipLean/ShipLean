import { useEffect, useRef, useState } from 'react';
import { shortcutLabel } from '../../quickSearch/host/shortcutLabel';
import { useQuickSearch } from '../../quickSearch/host/useQuickSearch';
import { useLocation } from 'react-router-dom';
import { matchRoute, routePath } from '../../../routes';
import { ProjectPicker } from '../ProjectPicker/ProjectPicker';
import { shellTestIds as ids } from '../testIds';
import { UserMenu } from '../UserMenu/UserMenu';
import { useShellProject } from '../useShellProject';
import { LevelsSubmenu } from './LevelsSubmenu';
import { NavItem } from './NavItem';
import { ProjectButton } from './ProjectButton';
import { backlogsRoutes, boardsRoutes, sprintsRoutes } from './sections';
import { SprintsSubmenu } from './SprintsSubmenu';

// The frame's left side: the ShipLean mark, the project button and its picker, the sections of the project and the
// signed-in user. Under 900px it becomes a strip across the top.
export function SideMenu() {
  const { pathname, search } = useLocation();
  const route = matchRoute(pathname, search);
  const { key, project } = useShellProject();
  const [picking, setPicking] = useState(false);
  const side = useRef<HTMLElement>(null);
  const button = useRef<HTMLButtonElement>(null);

  // Opening quick search puts the picker away.
  const { open: openQuickSearch, isOpen: searching } = useQuickSearch();
  const [sawSearching, setSawSearching] = useState(searching);
  if (sawSearching !== searching) {
    setSawSearching(searching);
    if (searching) setPicking(false);
  }

  useEffect(() => {
    if (!picking) return;
    const close = (event: MouseEvent) => {
      if (!side.current?.contains(event.target as Node)) setPicking(false);
    };
    document.addEventListener('mousedown', close);
    return () => document.removeEventListener('mousedown', close);
  }, [picking]);

  // The shortcut opens quick search in the same render that puts the picker away, and the dialog takes focus's way back
  // from whatever has it then. Moving focus to the project button first (capture runs ahead of the shortcut's own
  // listener) leaves it a control that stays.
  useEffect(() => {
    if (!picking) return;
    const onShortcut = (event: KeyboardEvent) => {
      if ((event.ctrlKey || event.metaKey) && !event.shiftKey && event.key.toLowerCase() === 'p') {
        button.current?.focus();
      }
    };
    window.addEventListener('keydown', onShortcut, true);
    return () => window.removeEventListener('keydown', onShortcut, true);
  }, [picking]);

  const closePicker = () => {
    setPicking(false);
    button.current?.focus();
  };
  const label = shortcutLabel();
  const is = (ids_: readonly string[]) => route !== undefined && ids_.includes(route.id);

  return (
    <aside
      ref={side}
      data-testid={ids.sideMenu}
      aria-label="Project navigation"
      className="relative z-20 flex w-(--sidebar-width) flex-none flex-col gap-0.5 border-r border-edge bg-sunken p-2 max-[900px]:w-auto max-[900px]:flex-row max-[900px]:flex-wrap max-[900px]:items-center max-[900px]:border-r-0 max-[900px]:border-b"
    >
      <div className="flex h-[26px] items-center justify-between max-[900px]:hidden">
        <span className="flex items-center gap-[7px] px-2 text-[length:var(--text-sm)] font-bold tracking-tight text-ink-muted">
          <i className="inline-block size-3.5 rounded-[4px] bg-signal" />
          ShipLean
        </span>
        <button
          type="button"
          data-testid={ids.searchButton}
          aria-label="Quick search"
          title={`Quick search (${label})`}
          className="flex h-[22px] items-center gap-1 rounded-control border border-edge bg-panel px-1.5 font-mono text-[length:var(--text-2xs)] text-ink-muted hover:border-signal hover:text-ink"
          onClick={openQuickSearch}
        >
          <SearchIcon className="size-3" />
          {label}
        </button>
      </div>
      <ProjectButton
        ref={button}
        projectKey={key}
        project={project}
        open={picking}
        onClick={() => setPicking(!picking)}
      />
      <button
        type="button"
        data-testid={ids.searchIconButton}
        aria-label="Quick search"
        title={`Quick search (${label})`}
        className="hidden size-[30px] items-center justify-center rounded-control border border-edge bg-panel text-ink-muted hover:border-signal hover:text-ink max-[900px]:flex"
        onClick={openQuickSearch}
      >
        <SearchIcon className="size-4" />
      </button>
      {key ? (
        <>
          <NavItem
            icon="☰"
            testId={ids.navBacklogs}
            to={routePath('storiesBacklog', { key })}
            active={is(backlogsRoutes)}
          >
            Backlogs
          </NavItem>
          {is(backlogsRoutes) ? (
            <LevelsSubmenu section="backlogs" projectKey={key} route={route} />
          ) : null}
          <NavItem
            icon="▦"
            testId={ids.navBoards}
            to={routePath('storiesBoard', { key })}
            active={is(boardsRoutes)}
          >
            Boards
          </NavItem>
          {is(boardsRoutes) ? (
            <LevelsSubmenu section="boards" projectKey={key} route={route} />
          ) : null}
          <NavItem
            icon="◷"
            testId={ids.navSprints}
            to={routePath('sprints', { key })}
            active={is(sprintsRoutes)}
          >
            Sprints
          </NavItem>
          {project && is(sprintsRoutes) ? (
            <SprintsSubmenu projectId={project.id} projectKey={key} route={route} />
          ) : null}
          <NavItem
            icon="⇪"
            testId={ids.navJiraImport}
            to={routePath('jiraImport', { key })}
            active={route?.id === 'jiraImport'}
          >
            Jira import
          </NavItem>
          <NavItem
            icon="⚙"
            testId={ids.navSettings}
            to={routePath('projectSettings', { key, tab: 'general' })}
            active={route?.id === 'projectSettings'}
          >
            Settings
          </NavItem>
        </>
      ) : null}
      <UserMenu />
      {picking ? <ProjectPicker currentKey={key} onClose={closePicker} /> : null}
    </aside>
  );
}

function SearchIcon({ className }: { className: string }) {
  return (
    <svg
      viewBox="0 0 16 16"
      className={className}
      fill="none"
      stroke="currentColor"
      strokeWidth="1.6"
      aria-hidden="true"
    >
      <circle cx="7" cy="7" r="4.5" />
      <path d="m10.5 10.5 3.5 3.5" strokeLinecap="round" />
    </svg>
  );
}
