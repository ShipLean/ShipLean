import type { ProjectDto } from '@shiplean/core/api/projects';
import { ToastHost } from '../../../../components/ToastHost';
import { SettingsDemos } from '../SettingsDemos';

// The Demos tab with the toasts its failures show.
export function SettingsDemosHarness({ project }: { project: ProjectDto }) {
  return (
    <>
      <SettingsDemos project={project} />
      <ToastHost />
    </>
  );
}
