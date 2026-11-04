import type { ProjectDto } from '../../../api/projects';
import { demosTestIds as ids } from '../../demos/testIds';
import { useDemoSettings } from '../../demos/demosApi';
import { BrandPanel } from './BrandPanel';
import { YouTubePanel } from './YouTubePanel';

// TODO: the Automatic switch, the environment text and this tab's own Save.
export function SettingsDemos({ project }: { project: ProjectDto }) {
  const query = useDemoSettings(project.id);
  const settings = query.data;
  const readOnly = project.role !== 'admin';
  return (
    <div data-testid={ids.demoSettingsPanel}>
      {settings === undefined ? (
        <p>{query.isError ? query.error.message : 'Loading the demo settings'}</p>
      ) : (
        <>
          <BrandPanel project={project} brand={settings.brand} readOnly={readOnly} />
          <YouTubePanel project={project} youtube={settings.youTube} readOnly={readOnly} />
        </>
      )}
    </div>
  );
}
