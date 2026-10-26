import type { CardFieldSettingJson } from '../../../../api/persisted';
import { boardSettingsTestIds as ids } from '../../testIds';
import type { CoreField } from './fieldEdits';

const core: { field: CoreField; label: string; testId: string }[] = [
  { field: 'showKey', label: 'Key', testId: ids.fieldsShowKey },
  { field: 'showAssignee', label: 'Assigned to', testId: ids.fieldsShowAssignee },
  { field: 'showTags', label: 'Tags', testId: ids.fieldsShowTags },
  { field: 'showPoints', label: 'Story points', testId: ids.fieldsShowPoints },
  { field: 'showEmptyFields', label: 'Show empty fields', testId: ids.fieldsShowEmpty },
];

/** The fixed fields of a card: each is shown or hidden. */
export function CoreFields({
  setting,
  readOnly,
  onChange,
}: {
  setting: CardFieldSettingJson;
  readOnly: boolean;
  onChange(next: CardFieldSettingJson): void;
}) {
  return (
    <fieldset
      data-testid={ids.fieldsCore}
      className="m-0 grid gap-1.5 rounded-panel border border-edge p-3"
    >
      <legend className="px-1 text-[length:var(--text-sm)] font-semibold">Core fields</legend>
      {core.map(({ field, label, testId }) => (
        <label key={field} className="flex items-center gap-2 text-[length:var(--text-sm)]">
          <input
            type="checkbox"
            data-testid={testId}
            checked={setting[field]}
            disabled={readOnly}
            onChange={(event) => onChange({ ...setting, [field]: event.target.checked })}
          />
          {label}
        </label>
      ))}
    </fieldset>
  );
}
