import { useEffect, useRef, useState } from 'react';
import { useLocation } from 'react-router-dom';
import { matchRoute, routePath } from '../../../routes';
import { ProjectPicker } from '../ProjectPicker/ProjectPicker';
import { shellTestIds as ids } from '../testIds';
import { UserMenu } from '../UserMenu/UserMenu';
import { useShellProject } from '../useShellProject';
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
  const searchInput = useRef<HTMLInputElement>(null);

  // Ctrl/⌘+P opens the picker, or puts the cursor back in its search when it is open. While a modal dialog is open
  // it does nothing.
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (!(event.ctrlKey || event.metaKey) || event.shiftKey || event.key.toLowerCase() !== 'p') {
        return;
      }
      if (document.querySelector('[aria-modal="true"]')) return;
      event.preventDefault();
      if (picking) searchInput.current?.focus();
      else setPicking(true);
    };
    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  }, [picking]);

  useEffect(() => {
    if (!picking) return;
    const close = (event: MouseEvent) => {
      if (!side.current?.contains(event.target as Node)) setPicking(false);
    };
    document.addEventListener('mousedown', close);
    return () => document.removeEventListener('mousedown', close);
  }, [picking]);

  const closePicker = () => {
    setPicking(false);
    button.current?.focus();
  };
  const is = (ids_: readonly string[]) => route !== undefined && ids_.includes(route.id);

  return (
    <aside
      ref={side}
      data-testid={ids.sideMenu}
      aria-label="Project navigation"
      className="relative z-20 flex w-(--sidebar-width) flex-none flex-col gap-0.5 border-r border-edge bg-sunken p-2 max-[900px]:w-auto max-[900px]:flex-row max-[900px]:flex-wrap max-[900px]:items-center max-[900px]:border-r-0 max-[900px]:border-b"
    >
      <span className="flex h-[26px] items-center gap-[7px] px-2 text-[length:var(--text-sm)] font-bold tracking-tight text-ink-muted max-[900px]:hidden">
        <i className="inline-block size-3.5 rounded-[4px] bg-signal" />
        ShipLean
      </span>
      <ProjectButton
        ref={button}
        projectKey={key}
        project={project}
        open={picking}
        onClick={() => setPicking(!picking)}
      />
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
          <NavItem
            icon="▦"
            testId={ids.navBoards}
            to={routePath('storiesBoard', { key })}
            active={is(boardsRoutes)}
          >
            Boards
          </NavItem>
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
      {picking ? (
        <ProjectPicker currentKey={key} searchRef={searchInput} onClose={closePicker} />
      ) : null}
    </aside>
  );
}
