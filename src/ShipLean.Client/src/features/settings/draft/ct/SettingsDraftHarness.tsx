import { useContext } from 'react';
import { useLocation } from 'react-router-dom';
import type { ProjectRole } from '../../../../api/projects';
import { projectSettingsTabs, type ProjectSettingsTab } from '../../../../routes';
import { projectDto } from './projectDto';
import { SettingsDraftContext } from '../LoadedSettings';
import { SettingsDraftSlot } from '../../../../slots/SettingsDraftSlot';

// Stands in for the settings page: the tab comes from the address, as the page reads it from the route.
// A tiny States consumer: stages one state change through the context, as the States tab will.
function FakeStates() {
  const context = useContext(SettingsDraftContext);
  if (context === undefined) throw new Error('no settings draft context');
  const { add } = context.draft.states;
  return (
    <div>
      <span data-testid="fake-states-count">{add.length}</span>
      <button
        type="button"
        data-testid="fake-states-add"
        onClick={() =>
          context.setDraft((d) => ({
            ...d,
            states: {
              ...d.states,
              add: [
                ...d.states.add,
                { id: 'new-state', type: 'story', name: 'Review', category: 'inProgress' },
              ],
            },
          }))
        }
      >
        add
      </button>
    </div>
  );
}

export function SettingsDraftHarness({
  role,
  fakeStates = false,
}: {
  role: ProjectRole;
  fakeStates?: boolean;
}) {
  const last = useLocation().pathname.split('/').pop();
  const tab: ProjectSettingsTab = projectSettingsTabs.find((t) => t === last) ?? 'general';
  return (
    <SettingsDraftSlot
      project={projectDto(role)}
      tab={tab}
      states={fakeStates ? <FakeStates /> : <div data-testid="harness-states">states</div>}
      integrations={<div data-testid="harness-integrations">integrations</div>}
    />
  );
}
