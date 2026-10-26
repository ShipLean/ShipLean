import type { CardFieldSettingJson } from '../../../../api/persisted';
import { Chip } from '../../../../components/Chip';
import {
  addExtra,
  extraFieldsFor,
  fieldLabel,
  isFull,
  isOverLimit,
  MAX_EXTRA_FIELDS,
  moveExtraUp,
  removeExtra,
} from '../../../boardSettings/panes/fields/fieldEdits';
import { taskboardSettingsTestIds as ids } from '../../testIds';

/** The extra fields of a card: ordered, at most ten, each added from the fields this type can show. */
export function ExtraFields({
  setting,
  readOnly,
  onChange,
}: {
  setting: CardFieldSettingJson;
  readOnly: boolean;
  onChange(next: CardFieldSettingJson): void;
}) {
  const full = isFull(setting);
  const left = extraFieldsFor(setting.type).filter(
    (f) => !setting.extraFields.includes(f.id) && !(setting.type === 'task' && f.id === 'state'),
  );
  return (
    <fieldset className="m-0 grid gap-1.5 rounded-panel border border-edge p-3">
      <legend className="flex items-center gap-1.5 px-1 text-[length:var(--text-sm)] font-semibold">
        Additional fields
        <span data-testid={ids.fieldsExtraCount}>
          <Chip tone={full ? 'warn' : 'muted'}>
            {setting.extraFields.length} / {MAX_EXTRA_FIELDS}
          </Chip>
        </span>
      </legend>
      {isOverLimit(setting) ? (
        <p
          data-testid={ids.fieldsOverLimit}
          role="alert"
          className="m-0 text-[length:var(--text-xs)] text-alert"
        >
          A {setting.type} card shows at most {MAX_EXTRA_FIELDS} extra fields: remove{' '}
          {setting.extraFields.length - MAX_EXTRA_FIELDS}.
        </p>
      ) : null}
      {setting.extraFields.length === 0 ? (
        <span className="text-[length:var(--text-sm)] text-ink-muted">None yet</span>
      ) : null}
      {setting.extraFields.map((id, index) => (
        <div
          key={id}
          data-testid={ids.fieldsExtraRow}
          className="flex h-[26px] items-center gap-1.5 text-[length:var(--text-sm)]"
        >
          <span className="w-3 text-[length:var(--text-xs)] text-ink-muted">{index + 1}</span>
          <span>{fieldLabel(id)}</span>
          <span className="flex-1" />
          <button
            type="button"
            data-testid={ids.fieldsExtraUp}
            aria-label={`Move ${fieldLabel(id)} up`}
            disabled={readOnly || index === 0}
            className={index === 0 ? 'invisible' : ''}
            onClick={() => onChange(moveExtraUp(setting, index))}
          >
            ↑
          </button>
          <button
            type="button"
            data-testid={ids.fieldsExtraRemove}
            aria-label={`Remove ${fieldLabel(id)}`}
            disabled={readOnly}
            onClick={() => onChange(removeExtra(setting, id))}
          >
            ✕
          </button>
        </div>
      ))}
      <select
        data-testid={ids.fieldsAdd}
        aria-label="Add a field"
        className="field mt-1 h-[26px] px-1 text-[length:var(--text-sm)]"
        value=""
        disabled={readOnly || full || left.length === 0}
        onChange={(event) => onChange(addExtra(setting, event.target.value))}
      >
        <option value="">+ Add field (up to {MAX_EXTRA_FIELDS})</option>
        {left.map((f) => (
          <option key={f.id} value={f.id}>
            {f.label}
          </option>
        ))}
      </select>
      {!full && left.length === 0 ? (
        <p
          data-testid={ids.fieldsAllAdded}
          className="m-0 text-[length:var(--text-xs)] text-ink-muted"
        >
          Every field this card can show is already added.
        </p>
      ) : null}
      {full ? (
        <p data-testid={ids.fieldsFull} className="m-0 text-[length:var(--text-xs)] text-ink-muted">
          A card shows at most {MAX_EXTRA_FIELDS} extra fields. Remove one to add another.
        </p>
      ) : null}
    </fieldset>
  );
}
