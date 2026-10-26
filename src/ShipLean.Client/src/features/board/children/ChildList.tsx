import { useCallback, useEffect, useRef } from 'react';
import type { WorkItemTypeId } from '../../../api/refs';
import type { Level } from '../../../routes';
import { boardTestIds as ids } from '../testIds';
import { useCloseOnHorizontalScroll } from '../useCloseOnHorizontalScroll';
import { AddChild } from './AddChild';
import { ChildRow } from './ChildRow';
import { useChildren } from './childrenApi';

/** Where the list sits (the roll-up's box) and what it lists. */
export interface OpenChildList {
  itemId: string;
  itemKey: string;
  type: WorkItemTypeId;
  left: number;
  top: number;
}

const HEADINGS: Record<WorkItemTypeId, string> = {
  epic: 'Epics',
  feature: 'Features',
  story: 'User stories',
  bug: 'Bugs',
  task: 'Tasks',
};

const WIDTH = 320;
const HEIGHT = 340;

// The children of a card, listed under its roll-up with their states and the ways to add one.
export function ChildList({
  open,
  projectId,
  projectKey,
  boardId,
  level,
  onClose,
}: {
  open: OpenChildList;
  projectId: string;
  projectKey: string;
  boardId: string;
  level: Level;
  /** `focusBack`: it was dismissed from the keyboard; focus returns to the roll-up. */
  onClose: (focusBack?: boolean) => void;
}) {
  const children = useChildren(projectId, open.itemId, open.type);
  const panel = useRef<HTMLDivElement>(null);
  // Focus moves into the list when it opens.
  useEffect(() => panel.current?.focus(), []);
  useEffect(() => {
    function onPointerDown(event: PointerEvent) {
      const target = event.target as Element;
      if (panel.current?.contains(target)) return;
      // Another roll-up opens its own list: the host swaps it in.
      if (target.closest(`[data-testid="${ids.cardRollup}"]`)) return;
      onClose();
    }
    function onKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') onClose(true);
    }
    document.addEventListener('pointerdown', onPointerDown);
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('pointerdown', onPointerDown);
      document.removeEventListener('keydown', onKeyDown);
    };
  }, [onClose]);
  // The list is fixed where it opened: when the board scrolls sideways it would hang over other cards.
  const itemId = open.itemId;
  const card = useCallback(
    () => document.querySelector(`[data-testid="${ids.card}"][data-item-id="${itemId}"]`),
    [itemId],
  );
  const closeIt = useCallback(() => onClose(), [onClose]);
  useCloseOnHorizontalScroll(card, closeIt);
  const left = Math.max(8, Math.min(open.left, window.innerWidth - WIDTH - 8));
  const top = Math.max(8, Math.min(open.top, window.innerHeight - HEIGHT - 8));
  return (
    <div
      ref={panel}
      role="dialog"
      aria-label={`Children of ${open.itemKey}`}
      tabIndex={-1}
      data-testid={ids.childList}
      data-item={open.itemKey}
      data-type={open.type}
      className="fixed z-[45] grid gap-1 outline-none rounded-panel border border-edge bg-panel p-2 text-[length:var(--text-sm)] shadow-lg"
      style={{ left, top, width: WIDTH, maxHeight: HEIGHT, overflowY: 'auto' }}
    >
      <div className="flex items-center gap-2">
        <b className="font-semibold">{open.itemKey}</b>
        <span className="text-ink-muted">{HEADINGS[open.type]}</span>
        <button
          type="button"
          data-testid={ids.childClose}
          aria-label="Close"
          className="btn-ghost ml-auto size-5 p-0"
          onClick={() => onClose(true)}
        >
          ×
        </button>
      </div>
      {children.isPending ? (
        <p data-testid={ids.childLoading} className="text-ink-muted">
          Loading…
        </p>
      ) : children.isError ? (
        <p data-testid={ids.childFailed} role="alert" className="text-alert">
          The children could not be loaded.
        </p>
      ) : children.data.length === 0 ? (
        <p data-testid={ids.childEmpty} className="text-ink-muted">
          Nothing below yet.
        </p>
      ) : (
        <ul className="grid gap-px">
          {children.data.map((child) => (
            <ChildRow key={child.id} child={child} projectKey={projectKey} />
          ))}
        </ul>
      )}
      <AddChild projectId={projectId} boardId={boardId} parentId={open.itemId} level={level} />
    </div>
  );
}
