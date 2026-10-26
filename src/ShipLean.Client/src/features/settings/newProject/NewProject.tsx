import { settingsTestIds as ids } from '../testIds';
import { NewProjectBlocked } from './NewProjectBlocked';
import { NewProjectFormView } from './NewProjectFormView';
import { useMeForNewProject } from './newProjectApi';

// The page body: the form, or the blocked page when the instance limits project creation to its admins and you are not one.
export function NewProject() {
  const me = useMeForNewProject();
  if (me.data === undefined) {
    return (
      <div data-testid={me.isError ? ids.loadFailed : ids.loading} className="p-4 text-ink-muted">
        {me.isError ? 'Could not load your account.' : 'Loading…'}
      </div>
    );
  }
  return me.data.canCreateProjects ? <NewProjectFormView me={me.data} /> : <NewProjectBlocked />;
}
