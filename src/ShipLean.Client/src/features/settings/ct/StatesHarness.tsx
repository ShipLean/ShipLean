import { useLocation } from 'react-router-dom';
import type { ProjectRole } from '../../../api/projects';
import { ToastHost } from '../../../components/ToastHost';
import { projectSettingsTabs, type ProjectSettingsTab } from '../../../routes';
import { SettingsDraftSlot } from '../../../slots/SettingsDraftSlot';
import { SettingsStatesSlot } from '../../../slots/SettingsStatesSlot';
import { projectDto } from '../draft/ct/projectDto';

// The settings frame with the real States tab on its States tab, as the settings page wires them; the tab comes from
// the address.
export function StatesHarness({ role }: { role: ProjectRole }) {
  const project = projectDto(role);
  const last = useLocation().pathname.split('/').pop();
  const tab: ProjectSettingsTab = projectSettingsTabs.find((t) => t === last) ?? 'states';
  return (
    <>
      <SettingsDraftSlot
        project={project}
        tab={tab}
        states={<SettingsStatesSlot project={project} tab={tab} />}
        integrations={<div />}
      />
      <ToastHost />
    </>
  );
}
