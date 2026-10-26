import { LINK_TYPES, type LinkType } from '../../../../api/persisted';
import { inputClass, selectClass } from '../../../workItem/form/inputClasses';
import { linkTypeLabels } from '../../../workItem/links/linkTypes';
import { featureTestIds as ids } from '../../testIds';

export type WorkItemLinkType = Exclude<LinkType, 'url'>;
const types = LINK_TYPES.filter((type): type is WorkItemLinkType => type !== 'url');

// A relation to another work item of this project: its key and how it relates. The server checks the key.
export function WorkItemTab({
  workItemKey,
  type,
  error,
  onKey,
  onType,
}: {
  workItemKey: string;
  type: WorkItemLinkType;
  error: string | null;
  onKey: (value: string) => void;
  onType: (value: WorkItemLinkType) => void;
}) {
  return (
    <>
      <div className="grid gap-2 sm:grid-cols-2">
        <label className="grid gap-1 text-[length:var(--text-xs)] text-ink-muted">
          Work item key
          <input
            data-testid={ids.addLinkKey}
            autoFocus
            placeholder="Such as SHIP-120"
            autoComplete="off"
            className={inputClass}
            value={workItemKey}
            onChange={(event) => onKey(event.target.value)}
          />
        </label>
        <label className="grid gap-1 text-[length:var(--text-xs)] text-ink-muted">
          Link type
          <select
            data-testid={ids.addLinkType}
            className={selectClass}
            value={type}
            onChange={(event) => onType(event.target.value as WorkItemLinkType)}
          >
            {types.map((option) => (
              <option key={option} value={option}>
                {linkTypeLabels[option]}
              </option>
            ))}
          </select>
        </label>
      </div>
      {error ? (
        <span
          data-testid={ids.addLinkError}
          role="alert"
          className="text-[length:var(--text-xs)] text-danger"
        >
          {error}
        </span>
      ) : null}
    </>
  );
}
