import { useState } from 'react';
import type { BoardSettingsJson } from '../../../api/persisted';
import type { WorkItemTypeId } from '../../../api/refs';
import type { PaneProps } from '../../../components/SettingsDialog/types';
import type { BoardSettingsContext } from '../panes';
import { boardSettingsTestIds as ids } from '../testIds';
import { CardPreview } from './fields/CardPreview';
import { CoreFields } from './fields/CoreFields';
import { ExtraFields } from './fields/ExtraFields';
import { withSetting } from './fields/fieldEdits';
import { TypeTabs } from './fields/TypeTabs';

/** What a card shows on this board, per work item type, with a preview that follows the draft. */
export function FieldsPane({
  draft,
  onChange,
  readOnly,
  context,
}: PaneProps<BoardSettingsJson, BoardSettingsContext>) {
  const types = draft.cardFields.map((s) => s.type);
  const [chosen, setChosen] = useState<WorkItemTypeId>();
  const setting = draft.cardFields.find((s) => s.type === chosen) ?? draft.cardFields[0];
  if (setting === undefined) return <div data-testid={ids.fieldsPane} />;
  const change = (next: typeof setting) => onChange(withSetting(draft, next));
  return (
    <div data-testid={ids.fieldsPane} className="grid content-start gap-3">
      <p className="m-0 text-[length:var(--text-xs)] text-ink-muted">
        Choose what shows on each card on this board, per work item type. Fields are edited on the
        card or in the work item form.
      </p>
      {types.length > 1 ? (
        <TypeTabs types={types} current={setting.type} onSelect={setChosen} />
      ) : null}
      <div className="grid grid-cols-[minmax(0,1fr)_minmax(0,16rem)] items-start gap-3">
        <div className="grid items-start gap-3 md:grid-cols-2">
          <CoreFields setting={setting} readOnly={readOnly} onChange={change} />
          <ExtraFields setting={setting} readOnly={readOnly} onChange={change} />
        </div>
        <CardPreview setting={setting} draft={draft} level={context.level} />
      </div>
    </div>
  );
}
