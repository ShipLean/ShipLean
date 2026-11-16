import { useId } from 'react';
import type { WorkItemDto } from '@shiplean/core/api/workItems';
import { workItemTestIds as ids } from '../testIds';
import { workItemAttachmentsKey } from '../workItemApi';
import { selectClass } from './inputClasses';
import { MarkdownSection } from './MarkdownSection';

export const severities = [
  { value: 1, label: '1 - Critical' },
  { value: 2, label: '2 - High' },
  { value: 3, label: '3 - Medium' },
  { value: 4, label: '4 - Low' },
] as const;

// A Bug's Repro steps (markdown) and Severity, which is separate from Priority.
export function ReproSection({
  item,
  repro,
  severity,
  reproChanged,
  severityChanged,
  foundBeforeRelease,
  foundBeforeReleaseChanged,
  onReproChange,
  onSeverityChange,
  onFoundBeforeReleaseChange,
}: {
  item: WorkItemDto;
  repro: string;
  severity: number | null;
  reproChanged: boolean;
  severityChanged: boolean;
  foundBeforeRelease: boolean;
  foundBeforeReleaseChanged: boolean;
  onReproChange: (reproSteps: string) => void;
  onSeverityChange: (severity: number | null) => void;
  onFoundBeforeReleaseChange: (foundBeforeRelease: boolean) => void;
}) {
  const severityId = useId();
  const foundId = useId();
  return (
    <div data-testid={ids.reproBlock} className="grid gap-3">
      <MarkdownSection
        title="Repro steps"
        field="reproSteps"
        emptyText="No repro steps yet."
        value={repro}
        savedValue={item.reproSteps}
        savedHtml={item.reproStepsHtml}
        changed={reproChanged}
        workItemId={item.id}
        attachmentsKey={workItemAttachmentsKey(item.projectId, item.key)}
        ids={{
          section: ids.repro,
          view: ids.reproView,
          edit: ids.reproEdit,
          input: ids.reproInput,
          preview: ids.reproPreview,
          pasting: ids.reproPasting,
          pasteError: ids.reproPasteError,
          externalNote: ids.reproExternalNote,
        }}
        onChange={onReproChange}
      />
      <div data-field="severity" className="flex items-center gap-2 touch:flex-wrap">
        <label
          htmlFor={severityId}
          className="flex items-center gap-1 text-[length:var(--text-sm)] font-semibold"
        >
          Severity
          {severityChanged ? (
            <span
              role="img"
              aria-label="Unsaved change"
              className="size-1.5 rounded-full bg-signal"
            />
          ) : null}
        </label>
        <select
          id={severityId}
          data-testid={ids.severity}
          className={`${selectClass} touch:h-9 touch:min-w-0 touch:flex-1`}
          value={severity ?? ''}
          onChange={(event) =>
            onSeverityChange(event.target.value === '' ? null : Number(event.target.value))
          }
        >
          <option value="">Not set</option>
          {severities.map((s) => (
            <option key={s.value} value={s.value}>
              {s.label}
            </option>
          ))}
        </select>
        <span className="text-[length:var(--text-xs)] text-ink-muted touch:basis-full">
          How bad it is for the people it hits. Priority is when we fix it.
        </span>
      </div>
      <div
        data-field="foundBeforeRelease"
        data-testid={ids.foundBeforeRelease}
        className="flex flex-wrap items-center gap-2"
      >
        <input
          id={foundId}
          type="checkbox"
          data-testid={ids.foundBeforeReleaseInput}
          className="size-4 accent-signal"
          checked={foundBeforeRelease}
          onChange={(event) => onFoundBeforeReleaseChange(event.target.checked)}
        />
        <label
          htmlFor={foundId}
          className="flex items-center gap-1 text-[length:var(--text-sm)] font-semibold"
        >
          Found before release
          {foundBeforeReleaseChanged ? (
            <span
              role="img"
              aria-label="Unsaved change"
              className="size-1.5 rounded-full bg-signal"
            />
          ) : null}
        </label>
        <span className="text-[length:var(--text-xs)] text-ink-muted touch:basis-full">
          Caught before the release it belonged to went out, so it did not escape.
        </span>
      </div>
    </div>
  );
}
