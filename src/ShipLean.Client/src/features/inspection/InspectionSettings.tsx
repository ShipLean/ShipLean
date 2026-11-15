import { useState } from 'react';
import type { ProjectDto } from '@shiplean/core/api/projects';
import { AgentSetup } from './AgentSetup';
import { InspectionSettingsGeneral } from './InspectionSettingsGeneral';
import { inspectionTestIds as ids } from './testIds';

const SUB_TABS = [
  { id: 'general', label: 'General', testId: ids.inspectionSubTabGeneral },
  { id: 'agent-setup', label: 'Agent setup', testId: ids.inspectionSubTabAgentSetup },
] as const;
type SubTab = (typeof SUB_TABS)[number]['id'];

// The Inspection project-settings tab: General and Agent setup sub-tabs and its own Save, outside the settings draft.
// TODO: Save is inert until the General form (it owns the fields) hands the frame its pending change.
export function InspectionSettings({ project }: { project: ProjectDto }) {
  const [tab, setTab] = useState<SubTab>('general');
  const readOnly = project.role !== 'admin';
  return (
    <div data-testid={ids.inspectionSettings} className="p-4">
      <div className="mb-4 flex items-center gap-2">
        <div data-testid={ids.inspectionSubTabs} role="tablist" className="flex gap-1">
          {SUB_TABS.map((sub) => (
            <button
              key={sub.id}
              type="button"
              role="tab"
              data-testid={sub.testId}
              aria-selected={tab === sub.id}
              onClick={() => setTab(sub.id)}
              className={`h-[30px] rounded-control px-3 text-[length:var(--text-sm)] ${
                tab === sub.id ? 'bg-signal-soft font-semibold text-ink' : 'text-ink-muted'
              }`}
            >
              {sub.label}
            </button>
          ))}
        </div>
        {readOnly ? null : (
          <button
            type="button"
            data-testid={ids.inspectionSettingsSave}
            disabled
            className="ml-auto h-[30px] rounded-control px-3 text-[length:var(--text-sm)]"
          >
            Save
          </button>
        )}
      </div>
      {tab === 'general' ? (
        <InspectionSettingsGeneral project={project} />
      ) : (
        <AgentSetup project={project} />
      )}
    </div>
  );
}
