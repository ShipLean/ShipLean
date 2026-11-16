import { settingsTestIds } from '../testIds';
import { FieldErrorText } from './FieldErrorText';
import { SEVERITY_KEYS, type SeverityKey } from './model';
import { Panel } from './Panel';
import type { TabProps } from './tabProps';

const input =
  'h-7 w-20 rounded-control border border-edge bg-panel px-2 font-mono text-[length:var(--text-sm)] text-ink disabled:opacity-60 touch:min-h-[var(--touch-target)] touch:w-full';

const LABELS: Record<SeverityKey, string> = {
  '1': '1 Critical',
  '2': '2 High',
  '3': '3 Medium',
  '4': '4 Low',
};

const TEST_IDS: Record<SeverityKey, string> = {
  '1': settingsTestIds.severityWeight1,
  '2': settingsTestIds.severityWeight2,
  '3': settingsTestIds.severityWeight3,
  '4': settingsTestIds.severityWeight4,
};

/** What an escaping bug of each severity counts for in Inspection's weighted bug number. */
export function SeverityWeightsPanel({ draft, setDraft, readOnly, errors }: TabProps) {
  return (
    <Panel title="Bug severity weights" testId={settingsTestIds.severityWeightsPanel}>
      <div className="grid gap-2 sm:grid-cols-4">
        {SEVERITY_KEYS.map((key) => (
          <label key={key} className="grid gap-1 text-[length:var(--text-sm)] font-medium text-ink">
            {LABELS[key]}
            <input
              id={`settings-field-bugSeverityWeight-${key}`}
              data-testid={TEST_IDS[key]}
              className={input}
              inputMode="numeric"
              value={draft.bugSeverityWeights[key]}
              disabled={readOnly}
              autoComplete="off"
              onChange={(e) =>
                setDraft((d) => ({
                  ...d,
                  bugSeverityWeights: { ...d.bugSeverityWeights, [key]: e.target.value },
                }))
              }
            />
            <FieldErrorText errors={errors} field={`bugSeverityWeight-${key}`} />
          </label>
        ))}
      </div>
      <span className="text-[length:var(--text-sm)] text-ink-muted">
        How much an escaping bug counts for in Inspection's weighted bug number. Whole numbers, at
        least 1; the default is 8, 4, 2 and 1.
      </span>
    </Panel>
  );
}
