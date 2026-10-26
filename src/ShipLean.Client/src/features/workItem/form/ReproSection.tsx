import type { WorkItemDto } from '../../../api/workItems';
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
  onReproChange,
  onSeverityChange,
}: {
  item: WorkItemDto;
  repro: string;
  severity: number | null;
  reproChanged: boolean;
  severityChanged: boolean;
  onReproChange: (reproSteps: string) => void;
  onSeverityChange: (severity: number | null) => void;
}) {
  return (
    <div className="grid gap-3">
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
      <div data-field="severity" className="flex items-center gap-2">
        <label
          htmlFor="work-item-severity"
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
          id="work-item-severity"
          data-testid={ids.severity}
          className={selectClass}
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
        <span className="text-[length:var(--text-xs)] text-ink-muted">
          How bad it is for the people it hits. Priority is when we fix it.
        </span>
      </div>
    </div>
  );
}
