import { useLocation } from 'react-router-dom';
import type { ProjectDto } from '../../../../../api/projects';
import { ToastHost } from '../../../../../components/ToastHost';
import { SettingsDemos } from '../../SettingsDemos';
import { harnessLocationTestId } from './harnessIds';

// The Demos tab with the toasts its failures show and the router's path and query (a return from Google clears its query).
export function YouTubePanelHarness({ project }: { project: ProjectDto }) {
  const { pathname, search } = useLocation();
  return (
    <>
      <output data-testid={harnessLocationTestId}>{pathname + search}</output>
      <SettingsDemos project={project} />
      <ToastHost />
    </>
  );
}
