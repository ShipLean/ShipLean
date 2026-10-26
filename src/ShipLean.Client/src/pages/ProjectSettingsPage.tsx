import { useParams } from 'react-router-dom';
import { pagesTestIds } from '../features/pages/testIds';
import { projectSettingsTabs, type ProjectSettingsTab } from '../routes';
import { SettingsDraftSlot } from '../slots/SettingsDraftSlot';
import { NotFoundState } from './NotFoundState';
import { pageRouteIds } from './pageRouteIds';
import { ProjectScreen } from './ProjectScreen';
import { projectSettingsSlots } from './projectSettingsSlots';

// A project settings tab, inside the settings draft's frame. A tab that does not exist is not found.
export function ProjectSettingsPage() {
  const { tab: raw } = useParams();
  const tab: ProjectSettingsTab | undefined = projectSettingsTabs.find((t) => t === raw);
  return (
    <ProjectScreen routeId={pageRouteIds.projectSettings} testId={pagesTestIds.projectSettingsPage}>
      {(project) =>
        tab === undefined ? (
          <NotFoundState />
        ) : (
          <SettingsDraftSlot {...projectSettingsSlots(project, tab).settingsDraft} />
        )
      }
    </ProjectScreen>
  );
}
