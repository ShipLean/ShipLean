import { Navigate, useParams, useSearchParams } from 'react-router-dom';
import { pagesTestIds } from '../features/pages/testIds';
import { projectSettingsTabs, routePath, type ProjectSettingsTab } from '../routes';
import { SettingsDraftSlot } from '../slots/SettingsDraftSlot';
import { useTouchShell } from '../touch/useTouchShell';
import { NotFoundState } from './NotFoundState';
import { pageRouteIds } from './pageRouteIds';
import { ProjectScreen } from './ProjectScreen';
import { projectSettingsSlots } from './projectSettingsSlots';

// A project settings tab, inside the settings draft's frame. A tab that does not exist is not found. The tab-less
// address is the pane index at touch (SHIP-1809 P1) and General above 900px, where the tabs are the index.
export function ProjectSettingsPage() {
  const { key = '', tab: raw } = useParams();
  const [search] = useSearchParams();
  const { touch } = useTouchShell();
  const tab: ProjectSettingsTab | undefined = projectSettingsTabs.find((t) => t === raw);
  if (raw === undefined && !touch)
    return (
      <Navigate
        replace
        to={routePath(
          'projectSettings',
          { key, tab: 'general' },
          { item: search.get('item') ?? undefined },
        )}
      />
    );
  return (
    <ProjectScreen
      routeId={raw === undefined ? pageRouteIds.projectSettingsIndex : pageRouteIds.projectSettings}
      testId={pagesTestIds.projectSettingsPage}
    >
      {(project) =>
        raw !== undefined && tab === undefined ? (
          <NotFoundState />
        ) : (
          <SettingsDraftSlot
            {...projectSettingsSlots(project, tab ?? 'general').settingsDraft}
            tab={tab}
          />
        )
      }
    </ProjectScreen>
  );
}
