import { useCallback, useState } from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import { ToastHost } from './components/ToastHost';
import { useProjectRealtime } from './realtime';
import { QuickSearchProvider } from './features/quickSearch/host/QuickSearchProvider';
import { SideMenu } from './features/shell/SideMenu/SideMenu';
import { Crumbs } from './features/shell/TopBar/Crumbs';
import { useAppearanceOnLoad } from './features/shell/useAppearanceOnLoad';
import { useRememberProject } from './features/shell/useRememberProject';
import { useShellProject } from './features/shell/useShellProject';
import { TopBarRealtime } from './shell/slots/TopBarRealtime';
import { appTestIds } from './testIds';
import { TopBarProvider } from './touch/TopBar';
import { TouchDrawer } from './touch/TouchDrawer';
import { TouchTopBar } from './touch/TouchTopBar';
import { useTouchShell } from './touch/useTouchShell';

// The frame every page after sign-in sits in: the side menu on the left, the top bar (crumbs, and the real-time slot
// on project pages) and the routed page. Install-level pages (New project, Instance settings, Your profile, Jira
// import) use it too; the side menu then keeps the project you came from. At 900px and below it is the touch shell
// instead (K4): the pinned touch top bar, the page in the document's own scroll, and the side menu in a drawer.
export function Shell() {
  const { key, project, onProjectRoute } = useShellProject();
  const { touch } = useTouchShell();
  useAppearanceOnLoad();
  useRememberProject(onProjectRoute ? project : undefined);
  useProjectRealtime(onProjectRoute ? key : undefined);
  return (
    <QuickSearchProvider>
      <TopBarProvider>{touch ? <TouchFrame /> : <DesktopFrame />}</TopBarProvider>
    </QuickSearchProvider>
  );
}

function DesktopFrame() {
  return (
    <div className="flex h-dvh bg-canvas text-ink">
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

// The drawer remembers the location it opened on, so any navigation (a tap in it, back, a link) closes it.
function TouchFrame() {
  const location = useLocation();
  const [openOn, setOpenOn] = useState<string | undefined>();
  const drawerOpen = openOn === location.key;
  const close = useCallback(() => setOpenOn(undefined), []);
  return (
    <div className="flex min-h-dvh flex-col bg-canvas text-ink">
      <TouchTopBar
        menuOpen={drawerOpen}
        onMenu={() => setOpenOn(drawerOpen ? undefined : location.key)}
      />
      <main data-testid={appTestIds.shellMain} className="flex-1">
        <Outlet />
      </main>
      {drawerOpen ? <TouchDrawer onClose={close} /> : null}
      <ToastHost />
    </div>
  );
}
