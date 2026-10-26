import type { ImportMappingDto } from '../../../api/import';
import { importTestIds as ids } from '../testIds';

const sectionId = (section: string) => `import-mapping-${section}`;

// Warnings first; each one takes you to its section. They never block the run.
export function MappingWarnings({ mapping }: { mapping: ImportMappingDto }) {
  const { totals } = mapping;
  const figures: [string, number][] = [
    ['issues', totals.issues],
    ['comments', totals.comments],
    ['attachments', totals.attachments],
    ['revisions', totals.revisions],
    ['sprints', totals.sprints],
    ['people', totals.people],
  ];
  return (
    <section
      data-testid={ids.mappingWarnings}
      aria-label="Warnings"
      className="panel flex flex-col gap-[var(--space-2)] p-[var(--space-4)]"
    >
      <header className="flex flex-wrap items-baseline gap-[var(--space-3)]">
        <h2 className="font-semibold">Review before the run</h2>
        <span data-testid={ids.mappingTotals} className="text-ink-muted">
          {figures.map(([label, n]) => `${n.toLocaleString('en-GB')} ${label}`).join(' · ')}
        </span>
      </header>
      <ul className="m-0 flex list-none flex-col gap-1 p-0">
        {mapping.warnings.map((w) => (
          <li
            key={`${w.section}-${w.message}`}
            data-testid={ids.mappingWarning}
            data-severity={w.severity}
            className="flex items-baseline gap-[var(--space-2)]"
          >
            <span aria-hidden="true">{w.severity === 'warning' ? '▲' : 'ⓘ'}</span>
            <span className="flex-1">{w.message}</span>
            <button
              type="button"
              data-testid={ids.mappingWarningLink}
              className="btn-ghost"
              onClick={() => {
                const target = document.getElementById(sectionId(w.section));
                target?.scrollIntoView({ block: 'start' });
                target?.focus();
              }}
            >
              Review
            </button>
          </li>
        ))}
      </ul>
    </section>
  );
}
