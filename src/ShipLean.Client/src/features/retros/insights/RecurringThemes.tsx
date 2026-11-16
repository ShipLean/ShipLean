import { useSearchParams } from 'react-router-dom';
import { useItemPeek } from '../../itemPeek/useItemPeek';
import { useTouchShell } from '../../../touch/useTouchShell';
import { MergeProposals } from './merge/MergeProposals';
import { RecurringCallout } from './recurring/RecurringCallout';
import { RecurringRow } from './recurring/RecurringRow';
import { UnthemedRow } from './recurring/UnthemedRow';
import { ThemeAxis } from './touch/ThemeAxis';
import { ThemeCard } from './touch/ThemeCard';
import { ThemeSheet, UnthemedSheet } from './touch/ThemeSheet';
import { defsSheetValue, withSheet } from './touch/insightsAddress';
import {
  themeSheetId,
  UNTHEMED_SHEET_ID,
  withoutSheet,
  withThemeSheet,
} from './touch/themeSheetAddress';
import { LastChangeHeading } from './tips/LastChangeHeading';
import { NoSprintTip } from './tips/NoSprintTip';
import { StatusHeading } from './tips/StatusHeading';
import type { RetroInsightsSlotProps } from './InsightsSlotProps';
import { retrosTestIds as ids } from '../testIds';

const HEAD = 'px-3 py-2 text-left text-[length:var(--text-xs)] font-semibold text-ink-muted';

/** Recurring themes: a table per theme with mentions per sprint or week (heat-shaded), No sprint, then Unthemed, total, status, action items, last change. */
export function RecurringThemes({ project, insights }: RetroInsightsSlotProps) {
  const { columns, themes, mode } = insights;
  const max = Math.max(0, ...themes.flatMap((t) => t.counts));
  const first = columns[0]?.label;
  const last = columns[columns.length - 1]?.label.replace(/^Sprint\s+/, '');
  const recurring = themes.filter((t) => t.status === 'recurring').length;
  const { touch } = useTouchShell();
  const [params, setParams] = useSearchParams();
  // An open item is shown over the page, so the sheet gives way to it and comes back with it closed.
  const { itemKey } = useItemPeek();
  const sheetId = touch ? themeSheetId(params) : null;
  const sheetRow = themes.find((t) => t.theme.id === sheetId);
  const unthemedOpen = sheetId === UNTHEMED_SHEET_ID && insights.unthemed.total > 0;
  const write = (next: URLSearchParams) => setParams(next, { replace: true });
  const closeSheet = () => write(withoutSheet(params));
  const range = first && last ? `, ${first} – ${last}` : '';
  const heading = (
    <header className="flex items-baseline gap-3 px-4 py-3">
      <h2 className="m-0 text-[length:var(--text-md)] font-semibold">
        Went badly and not in the plan
      </h2>
      <span className="text-[length:var(--text-sm)] text-ink-muted">by theme{range}</span>
    </header>
  );
  // At touch (RETRO-11): What these mean beside the heading, and the range with the recurring count under it.
  const touchHeading = (
    <header className="grid gap-0.5">
      <div className="flex items-baseline gap-3">
        <h2 className="m-0 min-w-0 flex-1 text-[length:var(--text-md)] font-semibold">
          Went badly and not in the plan
        </h2>
        <button
          type="button"
          data-testid={ids.retroThemesHeadingDefs}
          className="min-h-[var(--touch-target)] shrink-0 cursor-pointer text-[length:var(--text-sm)] text-signal"
          onClick={() => write(withSheet(params, defsSheetValue()))}
        >
          What these mean
        </button>
      </div>
      <p
        data-testid={ids.retroThemesHeadingSummary}
        className="m-0 text-[length:var(--text-sm)] text-ink-muted"
      >
        By theme{range}. {recurring} recurring.
      </p>
    </header>
  );
  return (
    <div className="flex flex-col gap-3">
      <RecurringCallout count={recurring} mode={mode} />
      <MergeProposals project={project} proposals={insights.mergeProposals} />
      {touch ? (
        <section className="grid grid-cols-[minmax(0,1fr)] gap-1.5">
          {touchHeading}
          <ThemeAxis columns={columns} />
          <div
            data-testid={ids.retroThemeCards}
            className="grid grid-cols-[repeat(auto-fill,minmax(min(100%,340px),1fr))] gap-2"
          >
            {themes.map((row) => (
              <ThemeCard
                key={row.theme.id}
                row={row}
                columns={columns}
                max={max}
                mode={mode}
                onOpen={(themeId) => write(withThemeSheet(params, themeId))}
              />
            ))}
            <UnthemedRow
              projectKey={project.key}
              unthemed={insights.unthemed}
              columns={columns}
              max={max}
              mode={mode}
            />
          </div>
          {sheetRow && !itemKey ? (
            <ThemeSheet
              project={project}
              row={sheetRow}
              columns={columns}
              mode={mode}
              impact={insights.impact}
              onClose={closeSheet}
              onSeeImpact={() => {
                const next = withoutSheet(params);
                next.set('view', 'impact');
                write(next);
              }}
              onMerge={(themeId) => {
                const next = withoutSheet(params);
                next.set('merge', themeId);
                write(next);
              }}
            />
          ) : null}
          {unthemedOpen && !itemKey ? (
            <UnthemedSheet
              project={project}
              unthemed={insights.unthemed}
              columns={columns}
              mode={mode}
              onClose={closeSheet}
            />
          ) : null}
        </section>
      ) : (
        <section className="rounded-frame border border-edge bg-panel">
          {heading}
          <div className="overflow-x-auto">
            <table data-testid={ids.retroRecurringTable} className="w-full border-collapse">
              <thead>
                <tr>
                  <th className={HEAD}>Theme</th>
                  {columns.map((column) => (
                    <th key={column.key} className={`${HEAD} text-center`}>
                      {column.label}
                    </th>
                  ))}
                  {mode === 'sprint' ? (
                    <th
                      data-testid={ids.retroInsightsNosprint}
                      className={`${HEAD} border-l-2 border-edge text-center`}
                    >
                      <NoSprintTip />
                    </th>
                  ) : null}
                  <th className={`${HEAD} text-right`}>Total</th>
                  <StatusHeading className={HEAD} mode={insights.mode} />
                  <th className={HEAD}>Action items</th>
                  <LastChangeHeading className={HEAD} mode={insights.mode} />
                </tr>
              </thead>
              <tbody>
                {themes.map((row) => (
                  <RecurringRow
                    key={row.theme.id}
                    projectId={project.id}
                    row={row}
                    columns={columns}
                    max={max}
                    mode={mode}
                  />
                ))}
                <UnthemedRow
                  projectKey={project.key}
                  unthemed={insights.unthemed}
                  columns={columns}
                  max={max}
                  mode={mode}
                />
              </tbody>
            </table>
          </div>
        </section>
      )}
    </div>
  );
}
