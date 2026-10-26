import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, type ReactNode } from 'react';
import type { ProjectDto } from '../../../api/projects';
import { registerInvalidation } from '../../../realtime';
import type { ProjectSettingsTab } from '../../../routes';
import { fetchProjectSettings, projectSettingsKey } from '../settingsApi';
import { settingsTestIds } from '../testIds';
import { LoadedSettings } from './LoadedSettings';

// Loads the project's settings and keeps them current: a teammate's save reads them again.
export function SettingsDraftFrame({
  project,
  tab,
  states,
  integrations,
}: {
  project: ProjectDto;
  tab: ProjectSettingsTab;
  states: ReactNode;
  integrations: ReactNode;
}) {
  const queryClient = useQueryClient();
  const query = useQuery({
    queryKey: projectSettingsKey(project.id),
    queryFn: () => fetchProjectSettings(project.id),
  });

  useEffect(
    () =>
      registerInvalidation('projectChanged', () => {
        void queryClient.invalidateQueries({ queryKey: projectSettingsKey(project.id) });
      }),
    [queryClient, project.id],
  );

  if (query.isPending) {
    return (
      <div data-testid={settingsTestIds.loading} className="p-4 text-ink-muted">
        Loading settings…
      </div>
    );
  }
  if (query.isError) {
    return (
      <div data-testid={settingsTestIds.loadFailed} role="alert" className="p-4 text-alert">
        The settings could not be loaded.
      </div>
    );
  }
  return (
    <LoadedSettings
      project={project}
      settings={query.data}
      tab={tab}
      states={states}
      integrations={integrations}
    />
  );
}
