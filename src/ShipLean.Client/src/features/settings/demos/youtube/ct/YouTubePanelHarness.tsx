import { useLocation, useNavigationType } from 'react-router-dom';
import type { ProjectDto } from '@shiplean/core/api/projects';
import { ToastHost } from '../../../../../components/ToastHost';
import { SettingsDemos } from '../../SettingsDemos';
import { harnessLocationTestId } from './harnessIds';

// The Demos tab with the toasts its failures show, and the router's path and query and how it got there:
// POP on mount, REPLACE when a return from Google clears its query, PUSH if that left a history entry behind.
export function YouTubePanelHarness({ project }: { project: ProjectDto }) {
  const { pathname, search } = useLocation();
  const navigationType = useNavigationType();
  return (
    <>
      <output data-testid={harnessLocationTestId} data-navigation={navigationType}>
        {pathname + search}
      </output>
      <SettingsDemos project={project} />
      <ToastHost />
    </>
  );
}
