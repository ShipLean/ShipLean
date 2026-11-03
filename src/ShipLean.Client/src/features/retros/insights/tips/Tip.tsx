import {
  useId,
  useLayoutEffect,
  useRef,
  useState,
  type HTMLAttributes,
  type ReactNode,
} from 'react';
import { createPortal } from 'react-dom';

const GAP = 6;
const EDGE = 8;

/** Wraps its children in a keyboard-reachable trigger; `tip` shows on hover and on focus, kept inside the window. */
export function Tip({
  tip,
  children,
  className,
  ...rest
}: { tip: ReactNode } & HTMLAttributes<HTMLSpanElement>) {
  const [open, setOpen] = useState(false);
  const trigger = useRef<HTMLSpanElement>(null);
  const bubble = useRef<HTMLDivElement>(null);
  const id = useId();

  useLayoutEffect(() => {
    if (!open || !trigger.current || !bubble.current) return;
    const box = trigger.current.getBoundingClientRect();
    const size = bubble.current.getBoundingClientRect();
    const left = Math.max(EDGE, Math.min(box.left, window.innerWidth - size.width - EDGE));
    const below = box.bottom + GAP;
    const top =
      below + size.height + EDGE > window.innerHeight
        ? Math.max(EDGE, box.top - size.height - GAP)
        : below;
    bubble.current.style.left = `${left}px`;
    bubble.current.style.top = `${top}px`;
    bubble.current.style.visibility = 'visible';
  }, [open]);

  return (
    <span
      {...rest}
      ref={trigger}
      tabIndex={0}
      aria-describedby={open ? id : undefined}
      className={`cursor-help underline decoration-dotted underline-offset-[3px] ${className ?? ''}`}
      onMouseEnter={() => setOpen(true)}
      onMouseLeave={() => setOpen(false)}
      onFocus={() => setOpen(true)}
      onBlur={() => setOpen(false)}
      onKeyDown={(e) => {
        if (e.key === 'Escape') setOpen(false);
      }}
    >
      {children}
      {open
        ? createPortal(
            <div
              ref={bubble}
              id={id}
              role="tooltip"
              style={{ visibility: 'hidden' }}
              className="pointer-events-none fixed top-0 left-0 z-60 max-w-[340px] rounded-control bg-ink px-2.5 py-2 text-left text-[length:var(--text-xs)] leading-[1.45] font-normal whitespace-normal text-canvas no-underline shadow-lg"
            >
              {tip}
            </div>,
            document.body,
          )
        : null}
    </span>
  );
}
