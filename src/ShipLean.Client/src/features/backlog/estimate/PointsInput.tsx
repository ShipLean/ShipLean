import { useEffect, useRef, useState } from 'react';
import { backlogTestIds as ids } from '../testIds';
import { parsePoints } from './useEstimate';

// The open points field of a row. Enter saves and walks down, Tab leaves as usual (and saves), Esc cancels. Empty
// clears the estimate; an invalid value stays in the field and nothing is sent.
export function PointsInput({
  rowKey,
  start,
  seeded,
  onCommit,
  onCancel,
}: {
  rowKey: string;
  start: string;
  seeded: boolean;
  onCommit: (value: number | null, walk: boolean) => void;
  onCancel: () => void;
}) {
  const [text, setText] = useState(start);
  const [invalid, setInvalid] = useState(false);
  const input = useRef<HTMLInputElement>(null);
  // Once the field has been settled, the blur that closing causes must not settle it again.
  const settled = useRef(false);

  useEffect(() => {
    const field = input.current;
    if (field === null) return;
    field.focus();
    if (seeded) field.setSelectionRange(start.length, start.length);
    else field.select();
  }, [seeded, start]);

  function commit(walk: boolean) {
    if (settled.current) return;
    const parsed = parsePoints(text);
    if (!parsed.ok) {
      setInvalid(true);
      return;
    }
    settled.current = true;
    onCommit(parsed.value, walk);
  }

  return (
    <input
      ref={input}
      data-testid={ids.pointsInput}
      aria-label={`Points for ${rowKey}`}
      aria-invalid={invalid}
      inputMode="decimal"
      value={text}
      className="h-[22px] w-[46px] rounded-control border border-accent bg-panel px-1 text-right font-semibold tabular-nums outline-none aria-invalid:border-alert"
      onChange={(event) => {
        setText(event.target.value);
        setInvalid(false);
      }}
      onKeyDown={(event) => {
        event.stopPropagation();
        if (event.key === 'Enter') {
          event.preventDefault();
          commit(true);
        } else if (event.key === 'Escape') {
          event.preventDefault();
          settled.current = true;
          onCancel();
        }
      }}
      onBlur={() => commit(false)}
    />
  );
}
