import { useState } from 'react';
import type { BoardSettingsJson } from '../../../api/persisted';
import type { WorkItemTypeId } from '../../../api/refs';
import type { PaneProps } from '../../../components/SettingsDialog/types';
import type { BoardSettingsContext } from '../../boardSettings/panes';
import { withSetting } from '../../boardSettings/panes/fields/fieldEdits';
import { taskboardSettingsTestIds as ids } from '../testIds';
import { CoreFields } from './fields/CoreFields';
import { ExtraFields } from './fields/ExtraFields';
import { TypeTabs } from './fields/TypeTabs';

/** What a card shows on the Taskboard, per work item type: task cards, and the story and bug row headers. */
export function FieldsPane({
  draft,
  onChange,
  readOnly,
}: PaneProps<BoardSettingsJson, BoardSettingsContext>) {
  const types = draft.cardFields.map((s) => s.type);
  const [chosen, setChosen] = useState<WorkItemTypeId>();
  const setting = draft.cardFields.find((s) => s.type === chosen) ?? draft.cardFields[0];
  if (setting === undefined) return <div data-testid={ids.fieldsPane} />;
  const change = (next: typeof setting) => onChange(withSetting(draft, next));
  return (
    <div data-testid={ids.fieldsPane} className="grid content-start gap-3">
      <p className="m-0 text-[length:var(--text-xs)] text-ink-muted">
        Choose what shows on each card on this Taskboard. Fields are edited on the card or in the
        work item form.
      </p>
      {types.length > 1 ? (
        <TypeTabs types={types} current={setting.type} onSelect={setChosen} />
      ) : null}
      <div className="grid items-start gap-3 md:grid-cols-2">
        <CoreFields setting={setting} readOnly={readOnly} onChange={change} />
        <ExtraFields setting={setting} readOnly={readOnly} onChange={change} />
      </div>
      {setting.type === 'task' ? null : (
        <label className="flex items-center gap-2 text-[length:var(--text-sm)]">
          <input
            type="checkbox"
            data-testid={ids.fieldsProgress}
            checked={draft.annotations.tasks}
            disabled={readOnly}
            onChange={(event) =>
              onChange({
                ...draft,
                annotations: { ...draft.annotations, tasks: event.target.checked },
              })
            }
          />
          Task progress (☑ done/total) on row headers
        </label>
      )}
    </div>
  );
}
