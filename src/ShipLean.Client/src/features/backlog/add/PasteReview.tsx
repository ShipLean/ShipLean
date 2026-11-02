import { useCallback, useEffect, useRef, useState } from 'react';
import { ApiRequestError } from '../../../api/client';
import {
  BULK_MAX_ROWS,
  type BacklogAddItem,
  type BacklogAddRequest,
  type BulkPlacement,
  type DuplicateItem,
} from '../../../api/backlogWrites';
import type { WorkItemTypeId } from '../../../api/refs';
import { Dialog } from '../../../components/Dialog';
import { showToast } from '../../../components/toastStore';
import { useBacklogAdd, useDuplicateCheck } from '../bulk/useBacklogAdd';
import { useSelection } from '../selection/useSelection';
import { backlogTestIds as ids } from '../testIds';
import { parsePaste, pointsError, pointsValue, type PasteRow as Row } from './pasteParse';
import { PasteRow } from './PasteRow';

const plural = (count: number, word: string) => `${count} ${word}${count === 1 ? '' : 's'}`;

// Where the new items land: just below the focused row, else the top.
function placementBelow(focusedId: string | null, order: readonly string[]): BulkPlacement | null {
  if (focusedId === null) return null;
  const next = order[order.indexOf(focusedId) + 1];
  return next === undefined
    ? { slot: 'last', beforeItemId: null }
    : { slot: 'before', beforeItemId: next };
}

function itemsOf(rows: readonly Row[]): BacklogAddItem[] {
  const items: BacklogAddItem[] = [];
  for (const row of rows) {
    const points = pointsValue(row.points);
    if (row.kind === 'task')
      items[items.length - 1]?.tasks.push({ title: row.title.trim(), points });
    else
      items.push({
        type: row.type,
        title: row.title.trim(),
        description: null,
        points,
        parentId: null,
        tasks: [],
      });
  }
  return items;
}

// The review of pasted lines: a grid to correct, and nothing is created until Create (or Keep as one item).
export function PasteReview({
  projectId,
  projectKey,
  initialText,
  defaultType,
  onClose,
}: {
  projectId: string;
  projectKey: string;
  initialText: string;
  defaultType: WorkItemTypeId;
  onClose: () => void;
}) {
  const [text, setText] = useState(initialText);
  const [rows, setRows] = useState(() => parsePaste(initialText, defaultType));
  const [duplicates, setDuplicates] = useState<Record<string, DuplicateItem>>({});
  const checked = useRef(new Set<string>());
  const add = useBacklogAdd(projectId, projectKey);
  const check = useDuplicateCheck(projectId);
  const selection = useSelection();

  // Asks about the titles not asked yet. A failed check only means no flags; it never blocks creating.
  const { mutate: askDuplicates } = check;
  const checkTitles = useCallback(
    (list: readonly Row[]) => {
      const titles = [
        ...new Set(list.filter((row) => row.kind === 'item').map((row) => row.title.trim())),
      ]
        .filter((title) => title !== '' && !checked.current.has(title))
        .slice(0, BULK_MAX_ROWS);
      if (titles.length === 0) return;
      titles.forEach((title) => checked.current.add(title));
      askDuplicates(
        { titles },
        {
          onSuccess: ({ matches }) =>
            setDuplicates((known) => {
              const next = { ...known };
              for (const match of matches) {
                const found = match.items[0];
                if (found) next[titles[match.index]] = found;
              }
              return next;
            }),
        },
      );
    },
    [askDuplicates],
  );
  // Once, on open: later checks are for edited titles.
  const opened = useRef(rows);
  useEffect(() => checkTitles(opened.current), [checkTitles]);

  const items = rows.filter((row) => row.kind === 'item').length;
  const tasks = rows.length - items;
  const over = rows.length > BULK_MAX_ROWS;
  const flagged = rows.some((row) => pointsError(row.points) !== null || row.title.trim() === '');
  const canCreate = rows.length > 0 && !over && !flagged && !add.isPending;
  const placement = placementBelow(selection.focusedId, selection.orderedIds);

  function send(request: Omit<BacklogAddRequest, 'sprintId' | 'placement'>) {
    add.mutate(
      { ...request, sprintId: null, placement },
      {
        onSuccess: ({ created }) => {
          showToast(`${plural(created.length, 'item')} added, from ${created[0]?.key ?? ''}`, {
            tone: 'ok',
          });
          onClose();
        },
      },
    );
  }

  function patch(id: number, change: Partial<Row>) {
    setRows((list) => list.map((row) => (row.id === id ? { ...row, ...change } : row)));
  }

  function leaveOut(id: number) {
    setRows((list) => {
      const at = list.findIndex((row) => row.id === id);
      if (at < 0) return list;
      let end = at + 1;
      if (list[at].kind === 'item') while (list[end]?.kind === 'task') end++;
      return [...list.slice(0, at), ...list.slice(end)];
    });
  }

  function keepAsOne() {
    const title = rows[0]?.title.trim() || text.trim().split(/\r?\n/)[0].trim();
    send({
      items: [
        {
          type: defaultType,
          title,
          description: text.trim(),
          points: null,
          parentId: null,
          tasks: [],
        },
      ],
    });
  }

  return (
    <Dialog
      title="Add many"
      testId={ids.pasteDialog}
      width="760px"
      onClose={onClose}
      footer={
        <>
          <button
            type="button"
            data-testid={ids.pasteCreate}
            className="btn-signal"
            disabled={!canCreate}
            onClick={() => send({ items: itemsOf(rows) })}
          >
            {rows.length === 0
              ? 'Add'
              : `Add ${plural(items, 'item')}${tasks ? ` and ${plural(tasks, 'task')}` : ''}`}
          </button>
          <button
            type="button"
            data-testid={ids.pasteKeepOne}
            className="btn-ghost"
            disabled={text.trim() === '' || add.isPending}
            onClick={keepAsOne}
          >
            Keep as one item
          </button>
          <span className="flex-1" />
          <button type="button" className="btn-ghost" onClick={onClose}>
            Cancel
          </button>
        </>
      }
    >
      <p className="m-0 text-[length:var(--text-sm)] text-ink-muted">
        Nothing is created until you press Add. One line, one item. An indented line is a task under
        the line above. <code>bug:</code> makes a bug. <code>(3 pts)</code> or a number column sets
        points. They keep the order you pasted.
      </p>
      <textarea
        data-testid={ids.pasteSource}
        aria-label="Pasted text"
        spellCheck={false}
        rows={4}
        className="field"
        value={text}
        onChange={(event) => {
          setText(event.target.value);
          setRows(parsePaste(event.target.value, defaultType));
        }}
        onBlur={() => checkTitles(rows)}
      />
      {over ? (
        <p
          data-testid={ids.bulkLimit}
          role="alert"
          className="m-0 text-[length:var(--text-sm)] text-alert"
        >
          That is {rows.length} rows counting tasks. {BULK_MAX_ROWS} is the most at once; take some
          out.
        </p>
      ) : null}
      <table className="w-full border-collapse text-[length:var(--text-sm)]">
        <thead>
          <tr className="text-left text-[length:var(--text-xs)] text-ink-muted">
            <th className="py-1 pr-2">Type</th>
            <th className="py-1 pr-2">Title</th>
            <th className="py-1 pr-2">Pts</th>
            <th />
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <PasteRow
              key={row.id}
              row={row}
              duplicate={row.kind === 'item' ? (duplicates[row.title.trim()] ?? null) : null}
              onChange={(change) => patch(row.id, change)}
              onTitleCommit={() => checkTitles(rows)}
              onRemove={() => leaveOut(row.id)}
            />
          ))}
        </tbody>
      </table>
      {add.error ? (
        <p
          data-testid={ids.addError}
          role="alert"
          className="m-0 text-[length:var(--text-sm)] text-alert"
        >
          {add.error instanceof ApiRequestError
            ? add.error.message
            : 'Could not add the items. Nothing was created.'}
        </p>
      ) : null}
    </Dialog>
  );
}
