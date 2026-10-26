import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import type { StateDto } from '../../../api/projects';
import type { WorkItemDto } from '../../../api/workItems';
import { StateDot } from '../../../components/StateDot';
import { StateMenu } from '../../../components/StateMenu';
import { sprintsTestIds as ids } from '../testIds';

/** What a row needs to change an item's state: the project's states, whether a change is in flight, and the change. */
export interface StateChanger {
  states: StateDto[];
  busy: boolean;
  onChange: (item: WorkItemDto, stateId: string) => void;
}

/** The row's state as a button that opens the item type's states; picking another one asks `onChange` for it. */
export function RowStateMenu({ item, changer }: { item: WorkItemDto; changer: StateChanger }) {
  const [open, setOpen] = useState(false);
  const [anchor, setAnchor] = useState<{ top: number; left: number; above: number } | null>(null);
  const root = useRef<HTMLSpanElement>(null);
  const menu = useRef<HTMLDivElement>(null);
  const button = useRef<HTMLButtonElement>(null);
  // Closing from the menu itself (a pick, Esc, Tab) hands focus back to the button that opened it.
  const closeToButton = () => {
    setOpen(false);
    button.current?.focus();
  };
  // The menu is placed against the viewport (in a portal) so the table's horizontal scroll cannot clip it.
  useEffect(() => {
    if (!open) return undefined;
    const close = () => setOpen(false);
    // A scroll that leaves the button where it was (the browser scrolling it into view as it is clicked) keeps the menu.
    const onScroll = () => {
      const box = root.current?.getBoundingClientRect();
      if (!box || box.bottom + 4 !== anchor?.top || box.left !== anchor.left) close();
    };
    const onPointer = (event: PointerEvent) => {
      const target = event.target as Node;
      if (!root.current?.contains(target) && !menu.current?.contains(target)) close();
    };
    document.addEventListener('pointerdown', onPointer);
    window.addEventListener('resize', close);
    // Capture: a scroll in the table's own wrapper does not bubble, and the menu would stay where the row was.
    window.addEventListener('scroll', onScroll, true);
    return () => {
      document.removeEventListener('pointerdown', onPointer);
      window.removeEventListener('resize', close);
      window.removeEventListener('scroll', onScroll, true);
    };
  }, [open, anchor]);
  // Below the button, or above it when the menu would run off the bottom of the viewport.
  useLayoutEffect(() => {
    const node = menu.current;
    if (!open || !anchor || !node) return;
    const height = node.getBoundingClientRect().height;
    const top = anchor.top + height > window.innerHeight ? anchor.above - height : anchor.top;
    node.style.top = `${Math.max(4, top)}px`;
  }, [open, anchor]);
  const toggle = () => {
    const box = root.current?.getBoundingClientRect();
    setAnchor(box ? { top: box.bottom + 4, left: box.left, above: box.top - 4 } : null);
    setOpen((was) => !was);
  };
  const options = changer.states
    .filter((state) => state.type === item.type.id && state.category !== 'removed')
    .sort((a, b) => a.order - b.order);
  return (
    <span ref={root} className="relative inline-flex">
      <button
        ref={button}
        type="button"
        data-testid={ids.stateButton}
        aria-label={`State of ${item.key}`}
        aria-haspopup="listbox"
        aria-expanded={open}
        disabled={changer.busy}
        className="inline-flex h-6 cursor-pointer items-center gap-1 rounded-control border border-transparent bg-transparent px-1 hover:border-edge hover:bg-signal-soft disabled:cursor-default disabled:opacity-60"
        onClick={toggle}
      >
        <StateDot category={item.state.category} name={item.state.name} />
        <span aria-hidden="true" className="text-ink-muted">
          ▾
        </span>
      </button>
      {open && anchor
        ? createPortal(
            <div ref={menu} style={{ top: anchor.top, left: anchor.left }} className="fixed z-30">
              <StateMenu
                states={options}
                currentId={item.state.id}
                onClose={closeToButton}
                onSelect={(stateId) => {
                  closeToButton();
                  if (stateId !== item.state.id) changer.onChange(item, stateId);
                }}
              />
            </div>,
            document.body,
          )
        : null}
    </span>
  );
}
