import {
  useId,
  useLayoutEffect,
  useRef,
  useState,
  type HTMLAttributes,
  type ReactNode,
} from 'react';
import { createPortal } from 'react-dom';
import { useSearchParams } from 'react-router-dom';
import { useTouchShell } from '../../../../touch/useTouchShell';
import { retrosTestIds } from '../../testIds';
import { defsSheetValue, withSheet } from '../touch/insightsAddress';
import type { DefsEntry } from './tipWording';

const GAP = 6;
const EDGE = 8;

/**
 * Wraps its children in a keyboard-reachable trigger; `tip` shows on hover and on focus, kept inside the window. At
 * 900px and below there is no hover: a tip with an `entry` is a tap that opens What these mean at that entry
 * (`sheet=defs:<entry>`), and the tap goes no further, so a card behind a chip doesn't open.
 */
export function Tip({
  tip,
  entry,
  children,
  className,
  ...rest
}: { tip: ReactNode; entry?: DefsEntry } & HTMLAttributes<HTMLSpanElement>) {
  const { touch } = useTouchShell();
  const [params, setParams] = useSearchParams();
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

  if (touch && entry !== undefined) {
    return (
      <span
        {...rest}
        role="button"
        tabIndex={0}
        aria-haspopup="dialog"
        className={`cursor-pointer ${className ?? ''}`}
        onClick={(event) => {
          event.stopPropagation();
          setParams(withSheet(params, defsSheetValue(entry)), { replace: true });
        }}
        onKeyDown={(event) => {
          if (event.key !== 'Enter' && event.key !== ' ') return;
          event.preventDefault();
          event.stopPropagation();
          setParams(withSheet(params, defsSheetValue(entry)), { replace: true });
        }}
      >
        {children}
      </span>
    );
  }

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
              data-testid={retrosTestIds.retroTipBubble}
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
