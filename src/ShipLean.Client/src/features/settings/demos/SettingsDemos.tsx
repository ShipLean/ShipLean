import type { ProjectDto } from '@shiplean/core/api/projects';
import { useDemoSettings } from '../../demos/demosApi';
import { BrandPanel } from './BrandPanel';
import { DemoSettingsForm } from './DemoSettingsForm';
import { YouTubePanel } from './YouTubePanel';

export function SettingsDemos({ project }: { project: ProjectDto }) {
  const query = useDemoSettings(project.id);
  const settings = query.data;
  const readOnly = project.role !== 'admin';
  return (
    <div className="p-4">
      {settings === undefined ? (
        <p>{query.isError ? query.error.message : 'Loading the demo settings'}</p>
      ) : (
        <>
          <DemoSettingsForm project={project} settings={settings} readOnly={readOnly} />
          <BrandPanel project={project} brand={settings.brand} readOnly={readOnly} />
          <YouTubePanel project={project} youtube={settings.youTube} readOnly={readOnly} />
        </>
      )}
    </div>
  );
}
