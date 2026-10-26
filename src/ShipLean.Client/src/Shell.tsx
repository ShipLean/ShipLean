import { Outlet } from 'react-router-dom';
import { ToastHost } from './components/ToastHost';
import { useProjectRealtime } from './realtime';
import { SideMenu } from './features/shell/SideMenu/SideMenu';
import { Crumbs } from './features/shell/TopBar/Crumbs';
import { useAppearanceOnLoad } from './features/shell/useAppearanceOnLoad';
import { useRememberProject } from './features/shell/useRememberProject';
import { useShellProject } from './features/shell/useShellProject';
import { TopBarRealtime } from './shell/slots/TopBarRealtime';
import { appTestIds } from './testIds';

// The frame every page after sign-in sits in: the side menu on the left, the top bar (crumbs, and the real-time slot
// on project pages) and the routed page. Install-level pages (New project, Instance settings, Your profile, Jira
// import) use it too; the side menu then keeps the project you came from.
export function Shell() {
  const { key, project, onProjectRoute } = useShellProject();
  useAppearanceOnLoad();
  useRememberProject(onProjectRoute ? project : undefined);
  useProjectRealtime(onProjectRoute ? key : undefined);
  return (
    <div className="flex h-dvh bg-canvas text-ink max-[900px]:h-auto max-[900px]:min-h-dvh max-[900px]:flex-col">
      <SideMenu />
      <div className="flex min-w-0 flex-1 flex-col">
        <header
          data-testid={appTestIds.shellTopBar}
          className="flex h-(--bar-height) flex-none items-center gap-3 border-b border-edge bg-panel px-4"
        >
          <Crumbs />
          <span className="flex-1" />
          <TopBarRealtime />
        </header>
        <main data-testid={appTestIds.shellMain} className="min-h-0 flex-1 overflow-auto">
          <Outlet />
        </main>
      </div>
      <ToastHost />
    </div>
  );
}
