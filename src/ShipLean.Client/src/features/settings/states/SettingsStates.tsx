import { useQueryClient } from '@tanstack/react-query';
import { Fragment, useContext, useEffect, useState } from 'react';
import type { StateDto } from '../../../api/projects';
import { WORK_ITEM_TYPE_IDS, type WorkItemTypeId } from '../../../api/refs';
import { registerInvalidation } from '../../../realtime';
import { showToast } from '../../../components/toastStore';
import { SettingsDraftContext } from '../draft/LoadedSettings';
import { Panel } from '../draft/Panel';
import { projectSettingsKey } from '../settingsApi';
import { settingsTestIds } from '../testIds';
import { AddStateForm } from './AddStateForm';
import { DeleteStateForm } from './DeleteStateForm';
import { StateRow } from './StateRow';
import {
  addState,
  defaultTarget,
  deleteBlock,
  editState,
  itemsIn,
  moveState,
  removeState,
  rowsOf,
  typeProblems,
  type StateRowModel,
} from './stateModel';
import { TypeSegment, typeLabel } from './TypeSegment';
import { useStateColumns } from './useStateColumns';

const BOARD_OF: Record<Exclude<WorkItemTypeId, 'task'>, string> = {
  epic: 'Epics board',
  feature: 'Features board',
  story: 'Stories board',
  bug: 'Stories board',
};

const EVENTS = [
  'statesChanged',
  'workItemCreated',
  'workItemUpdated',
  'workItemMoved',
  'workItemRemoved',
] as const;

const countsOf = (states: StateDto[], changes: Parameters<typeof rowsOf>[1]) =>
  Object.fromEntries(
    WORK_ITEM_TYPE_IDS.map((t) => [t, rowsOf(states, changes, t).length]),
  ) as Record<WorkItemTypeId, number>;

// The States tab: the states of one work item type at a time, edited into the settings draft (never saved on their
// own). Rename, recategorise, reorder, add and delete are changes in `draft.states`; Save applies them with the rest.
export function SettingsStates({ projectId }: { projectId: string }) {
  const context = useContext(SettingsDraftContext);
  const queryClient = useQueryClient();
  const [type, setType] = useState<WorkItemTypeId>('story');
  const [deleting, setDeleting] = useState<string>();
  const [seenFocus, setSeenFocus] = useState(context?.focus);
  const columns = useStateColumns(projectId, type);

  // Save jumped to a problem on another type: show that type so the field is there to focus.
  if (context !== undefined && context.focus !== seenFocus) {
    setSeenFocus(context.focus);
    if (context.focus?.tab === 'states' && context.focus.type !== undefined) {
      setType(context.focus.type);
      setDeleting(undefined);
    }
  }

  // Item counts come with the settings: a state's count changes with any work item write, its states with a teammate's save.
  useEffect(() => {
    const refresh = () => {
      void queryClient.invalidateQueries({ queryKey: projectSettingsKey(projectId) });
    };
    const offs = EVENTS.map((event) => registerInvalidation(event, refresh));
    return () => offs.forEach((off) => off());
  }, [queryClient, projectId]);

  if (context === undefined) throw new Error('The States tab belongs inside the settings draft.');
  const { setDraft, readOnly, settings, errors } = context;
  const erroredTypes = new Set(
    errors.flatMap((e) => (e.tab === 'states' && e.type ? [e.type] : [])),
  );
  const saved = settings.states;
  const changes = context.draft.states;
  const rows = rowsOf(saved, changes, type);
  const { rowErrors, typeError } = typeProblems(rows);
  const firstProposed = rows.find((r) => r.category === 'proposed');
  const columnOf = (row: StateRowModel) => {
    if (row.category === 'removed') return 'Hidden (Removed)';
    if (type === 'task') return row.name.trim() || 'New column';
    return columns === undefined ? '' : (columns.get(row.id) ?? undefined);
  };
  const stage = (next: (c: typeof changes) => typeof changes) =>
    setDraft((d) => ({ ...d, states: next(d.states) }));

  const remove = (id: string, targetId: string) => {
    const row = rows.find((r) => r.id === id);
    const target = rows.find((r) => r.id === targetId);
    setDeleting(undefined);
    stage((c) => removeState(c, id, targetId));
    if (row === undefined) return;
    const moved = itemsIn(row);
    showToast(
      moved === 0 || target === undefined
        ? `${row.name} will be deleted on save`
        : `${row.name} will be deleted on save · ${moved} ${moved === 1 ? 'item moves' : 'items move'} to ${target.name}`,
    );
  };
  const startDelete = (id: string) => {
    const row = rows.find((r) => r.id === id);
    const target = defaultTarget(rows, id);
    if (row === undefined || target === undefined) return;
    if (itemsIn(row) === 0) remove(id, target);
    else setDeleting(id);
  };

  return (
    <Panel title="States" testId={settingsTestIds.statesPanel}>
      <TypeSegment
        active={type}
        counts={countsOf(saved, changes)}
        errored={erroredTypes}
        onPick={(next) => {
          setDeleting(undefined);
          setType(next);
        }}
      />
      <div className="text-[length:var(--text-sm)] text-ink-muted">
        <b className="text-ink">{typeLabel(type)}</b> · {rows.length} states. The order is the order
        in state menus. New items start in the first Proposed state.
      </div>
      {typeError === undefined ? null : (
        <div
          role="alert"
          data-testid={settingsTestIds.stateTypeError}
          className="text-[length:var(--text-sm)] text-alert"
        >
          {typeError}
        </div>
      )}
      <table className="w-full text-[length:var(--text-sm)]">
        <thead>
          <tr className="text-left text-ink-muted">
            <th className="px-3 py-1 font-normal">Order</th>
            <th className="px-3 py-1 font-normal">State</th>
            <th className="px-3 py-1 font-normal">Category</th>
            <th className="px-3 py-1 text-right font-normal">Items</th>
            <th className="px-3 py-1 font-normal">
              {type === 'task' ? 'Taskboard column' : `Column on the ${BOARD_OF[type]}`}
            </th>
            <th />
          </tr>
        </thead>
        <tbody>
          {rows.map((row, position) => (
            <Fragment key={row.id}>
              <StateRow
                row={row}
                position={position}
                count={rows.length}
                readOnly={readOnly}
                error={rowErrors.get(row.id)}
                start={row.id === firstProposed?.id}
                column={columnOf(row)}
                blocked={deleteBlock(rows, row.id)}
                onName={(name) => stage((c) => editState(c, saved, row.id, { name }))}
                onCategory={(category) => stage((c) => editState(c, saved, row.id, { category }))}
                onMove={(by) => stage((c) => moveState(c, saved, type, row.id, by))}
                onDelete={() => startDelete(row.id)}
              />
              {deleting === row.id ? (
                <tr>
                  <td colSpan={6} className="p-0">
                    <DeleteStateForm
                      row={row}
                      targets={rows.filter((r) => r.id !== row.id)}
                      initial={defaultTarget(rows, row.id) ?? ''}
                      onConfirm={(target) => remove(row.id, target)}
                      onCancel={() => setDeleting(undefined)}
                    />
                  </td>
                </tr>
              ) : null}
            </Fragment>
          ))}
        </tbody>
      </table>
      {readOnly ? null : (
        <AddStateForm
          taken={(name) => rows.some((r) => r.name.trim().toLowerCase() === name.toLowerCase())}
          onAdd={(name, category) =>
            stage((c) => addState(c, saved, { id: crypto.randomUUID(), type, name, category }))
          }
        />
      )}
    </Panel>
  );
}
