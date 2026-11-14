import { useState } from 'react';
import { Link } from 'react-router-dom';
import type { ProjectDto } from '@shiplean/core/api/projects';
import type {
  RetroImpactDto,
  RetroInsightColumnDto,
  RetroInsightsMode,
  RetroThemeInsightDto,
} from '@shiplean/core/api/retros';
import { Chip } from '../../../../components/Chip';
import { routePath } from '../../../../routes';
import { Sheet } from '../../../../touch/Sheet';
import { ActionChip } from '../../../retroShared/ActionChip';
import { useRetroActionDialog } from '../../../retroShared/actionDialog/useRetroActionDialog';
import { retroCategoryLabels } from '../../../retroShared/retroCategories';
import { retrosTestIds as ids } from '../../testIds';
import { useRetroActionDialogData, useRetroSearch } from '../../useRetroQueries';
import { heatShade } from '../recurring/heatShade';
import { useRenameTheme } from '../recurring/useRenameTheme';
import { statusWording, verdictWording } from '../tips/tipWording';
import { ACTION_CHIPS_AT_TOUCH } from './actionChips';
import { RenameThemeForm } from './RenameThemeForm';
import { statusLabels, verdictLabels } from './themeLabels';

const MENTIONS_SHOWN = 3;
const GROUP =
  'm-0 pt-2.5 text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase';
const WIDE_BUTTON =
  'flex min-h-10 w-full cursor-pointer items-center justify-center rounded-control border border-edge bg-panel px-3 text-[length:var(--text-base)] text-ink no-underline';

const average = (n: number) => String(Number(n.toFixed(1)));

/**
 * Everything about one theme (D4), open while the address says `sheet=theme:<id>`: what its status means, its count for
 * every shown column, its action items, its last change, its latest mentions, and Rename… and Merge into another theme….
 * Rename swaps the body for the name field with Discard and Save in the footer (D9).
 */
export function ThemeSheet({
  project,
  row,
  columns,
  mode,
  impact,
  onClose,
  onSeeImpact,
  onMerge,
}: {
  project: ProjectDto;
  row: RetroThemeInsightDto;
  columns: readonly RetroInsightColumnDto[];
  mode: RetroInsightsMode;
  impact: readonly RetroImpactDto[];
  onClose: () => void;
  onSeeImpact: () => void;
  onMerge: (themeId: string) => void;
}) {
  const { theme } = row;
  const [draft, setDraft] = useState<string | null>(null);
  const { rename, pending, error, clearError } = useRenameTheme(project.id, theme.id, () =>
    setDraft(null),
  );
  const name = (draft ?? '').trim();
  const save = () => {
    if (name !== '' && name !== theme.name && !pending) rename(name);
  };
  const discard = () => {
    setDraft(null);
    clearError();
  };

  if (draft !== null) {
    return (
      <Sheet
        open
        title={theme.name}
        onClose={onClose}
        footer={
          <>
            <button
              type="button"
              data-testid={ids.retroThemeRenameDiscard}
              className={`${WIDE_BUTTON} flex-1`}
              onClick={discard}
            >
              Discard
            </button>
            <button
              type="button"
              data-testid={ids.retroThemeRenameSave}
              disabled={name === '' || name === theme.name || pending}
              className="flex min-h-10 flex-1 cursor-pointer items-center justify-center rounded-control border border-signal bg-signal px-3 text-[length:var(--text-base)] font-semibold text-signal-ink disabled:cursor-default disabled:opacity-50"
              onClick={save}
            >
              Save
            </button>
          </>
        }
      >
        <RenameThemeForm
          value={draft}
          error={error}
          onChange={setDraft}
          onSave={save}
          onDiscard={discard}
        />
      </Sheet>
    );
  }
  return (
    <Sheet open title={theme.name} onClose={onClose}>
      <div data-testid={ids.retroThemeSheet} data-theme-id={theme.id} className="grid gap-1">
        <StatusPart row={row} mode={mode} />
        <CountsPart row={row} columns={columns} mode={mode} />
        <ActionsPart project={project} row={row} onClose={onClose} />
        <LastChangePart row={row} impact={impact} mode={mode} onSeeImpact={onSeeImpact} />
        <MentionsPart project={project} row={row} />
        <div className="grid gap-2 pt-3">
          <button
            type="button"
            data-testid={ids.retroThemeSheetRename}
            className={WIDE_BUTTON}
            onClick={() => setDraft(theme.name)}
          >
            Rename…
          </button>
          <button
            type="button"
            data-testid={ids.retroThemeSheetMerge}
            className={WIDE_BUTTON}
            onClick={() => onMerge(theme.id)}
          >
            Merge into another theme…
          </button>
        </div>
      </div>
    </Sheet>
  );
}

function StatusPart({ row, mode }: { row: RetroThemeInsightDto; mode: RetroInsightsMode }) {
  const { label, tone } = statusLabels[row.status];
  return (
    <section
      data-testid={ids.retroThemeSheetStatus}
      data-status={row.status}
      className="grid gap-1.5"
    >
      <h3 className={GROUP}>Status</h3>
      <div className="flex items-start gap-2 text-[length:var(--text-sm)]">
        <Chip tone={tone}>{label}</Chip>
        <span className="min-w-0 text-ink-muted">{statusWording(row.status, mode)}</span>
      </div>
    </section>
  );
}

function CountsPart({
  row,
  columns,
  mode,
}: {
  row: RetroThemeInsightDto;
  columns: readonly RetroInsightColumnDto[];
  mode: RetroInsightsMode;
}) {
  const max = Math.max(0, ...row.counts);
  return (
    <section className="grid gap-1.5">
      <h3 className={GROUP}>Mentions per {mode === 'week' ? 'week' : 'sprint'}</h3>
      <div
        className="grid gap-[3px]"
        style={{ gridTemplateColumns: `repeat(${columns.length}, minmax(0, 1fr))` }}
      >
        {columns.map((column, i) => {
          const count = row.counts[i] ?? 0;
          return (
            <div
              key={column.key}
              data-testid={ids.retroThemeSheetCount}
              data-count={count}
              data-column-key={column.key}
              className="grid gap-[3px] text-center"
            >
              <span
                className={`grid h-[34px] place-items-center rounded-[4px] bg-edge/35 text-[length:var(--text-sm)] tabular-nums ${count === 0 ? 'text-ink-muted' : 'font-semibold'}`}
                style={count > 0 ? { background: heatShade(count, max) } : undefined}
              >
                {count}
              </span>
              <small className="text-[length:var(--text-2xs)] leading-tight text-ink-muted">
                {column.label.replace(/^Sprint\s+/, '')}
              </small>
            </div>
          );
        })}
      </div>
      <div className="flex flex-wrap justify-end gap-x-3.5 gap-y-1 text-[length:var(--text-sm)] text-ink-muted">
        {mode === 'sprint' ? (
          <span data-testid={ids.retroThemeSheetNoSprint} data-count={row.noSprint}>
            No sprint <b className="text-ink">{row.noSprint}</b>
          </span>
        ) : null}
        <span data-testid={ids.retroThemeSheetTotal} data-count={row.total}>
          Total <b className="text-ink">{row.total}</b>
        </span>
      </div>
    </section>
  );
}

function ActionsPart({
  project,
  row,
  onClose,
}: {
  project: ProjectDto;
  row: RetroThemeInsightDto;
  onClose: () => void;
}) {
  const dialog = useRetroActionDialog();
  const open = useRetroActionDialogData(project.id, { themeId: row.theme.id });
  const points = open.data?.selected.length;
  return (
    <section className="grid gap-1.5">
      <h3 className={GROUP}>Action items</h3>
      {row.actionItems.length === 0 ? (
        <p className="m-0 text-[length:var(--text-sm)] text-ink-muted">
          No action item is addressing this theme.
        </p>
      ) : (
        <ul className={`m-0 grid list-none gap-1 p-0 ${ACTION_CHIPS_AT_TOUCH}`}>
          {row.actionItems.map((action) => (
            <li
              key={action.workItemId}
              data-testid={ids.retroThemeSheetAction}
              className="flex items-center gap-2 text-[length:var(--text-sm)]"
            >
              <ActionChip action={action} />
              <span className="min-w-0 flex-1">{action.title}</span>
            </li>
          ))}
        </ul>
      )}
      <button
        type="button"
        data-testid={ids.retroThemeSheetAddAction}
        className={`${WIDE_BUTTON} border-dashed text-ink-muted`}
        onClick={() => {
          // The dialog opens over the page, under this sheet, so the sheet gives way first.
          onClose();
          dialog.open({ themeId: row.theme.id, scope: { kind: 'project' } });
        }}
      >
        {points
          ? `+ Action item on ${points} open point${points === 1 ? '' : 's'}`
          : '+ Action item'}
      </button>
    </section>
  );
}

function LastChangePart({
  row,
  impact,
  mode,
  onSeeImpact,
}: {
  row: RetroThemeInsightDto;
  impact: readonly RetroImpactDto[];
  mode: RetroInsightsMode;
  onSeeImpact: () => void;
}) {
  const unit = mode === 'week' ? 'week' : 'sprint';
  const verdict = row.lastVerdict;
  // The last change is the theme's action item that closed latest.
  const change = impact
    .filter((entry) => entry.theme.id === row.theme.id)
    .reduce<RetroImpactDto | null>(
      (last, entry) => (last && last.closedAt > entry.closedAt ? last : entry),
      null,
    );
  return (
    <section data-testid={ids.retroThemeSheetLastChange} className="grid gap-1.5">
      <h3 className={GROUP}>Last change</h3>
      {verdict === null ? (
        <p className="m-0 text-[length:var(--text-sm)] text-ink-muted">
          No change has shipped for this theme.
        </p>
      ) : (
        <>
          <div className="flex items-start gap-2 text-[length:var(--text-sm)]">
            <Chip tone={verdictLabels[verdict].tone}>{verdictLabels[verdict].label}</Chip>
            <span className="min-w-0 text-ink-muted">{verdictWording(verdict, mode)}</span>
          </div>
          {change ? (
            <div className="grid gap-1 text-[length:var(--text-sm)]">
              <div className={`flex items-center gap-2 ${ACTION_CHIPS_AT_TOUCH}`}>
                <ActionChip action={change.action} />
                <span className="min-w-0 flex-1">{change.action.title}</span>
              </div>
              <span className="text-ink-muted">
                Mentions per {unit}: {average(change.beforeAvg)} before
                {change.afterAvg === null
                  ? ', none after yet'
                  : `, ${average(change.afterAvg)} after`}
              </span>
            </div>
          ) : null}
          <button
            type="button"
            data-testid={ids.retroThemeSheetSeeImpact}
            className={WIDE_BUTTON}
            onClick={onSeeImpact}
          >
            See it on Impact
          </button>
        </>
      )}
    </section>
  );
}

function MentionsPart({ project, row }: { project: ProjectDto; row: RetroThemeInsightDto }) {
  const search = useRetroSearch(project.id, { theme: row.theme.id });
  const latest = search.data?.rows.slice(0, MENTIONS_SHOWN) ?? [];
  const total = search.data?.total ?? row.total;
  return (
    <section className="grid gap-0.5">
      <h3 className={GROUP}>Latest mentions</h3>
      {latest.map((point) => (
        <div
          key={point.id}
          data-testid={ids.retroThemeSheetMention}
          className="grid gap-0.5 border-b border-edge/60 py-2 text-[length:var(--text-sm)]"
        >
          <span>{point.text}</span>
          <small className="text-[length:var(--text-xs)] text-ink-muted">
            {retroCategoryLabels[point.category]} · {point.sprint?.name ?? 'No sprint'} ·{' '}
            {point.author.agentName ?? point.author.user.name}
          </small>
        </div>
      ))}
      <Link
        data-testid={ids.retroThemeSheetAllInSearch}
        to={routePath('retros', { key: project.key }, { theme: row.theme.id })}
        className="inline-flex min-h-[var(--touch-target)] items-center text-[length:var(--text-sm)] text-signal no-underline"
      >
        All {total} in Search
      </Link>
    </section>
  );
}
