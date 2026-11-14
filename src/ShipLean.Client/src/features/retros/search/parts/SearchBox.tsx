import { useEffect, useRef, useState } from 'react';
import { useDebouncedValue } from '../../../workItem/form/description/useDebouncedValue';
import { retrosTestIds as ids } from '../../testIds';

const DEBOUNCE_MS = 300;

/**
 * The search text. It keeps what is typed, writes it to the address once typing settles, and follows the address when
 * something else changes it (Clear, Back).
 */
export function SearchBox({
  value,
  onWrite,
  touch = false,
}: {
  value: string;
  onWrite: (q: string | undefined) => void;
  /** At touch: full width, a Search key on the keyboard, and Enter closes the keyboard (results follow typing anyway). */
  touch?: boolean;
}) {
  const [text, setText] = useState(value);
  const settled = useDebouncedValue(text, DEBOUNCE_MS);
  const lastWritten = useRef(value);
  const lastSettled = useRef(settled);

  useEffect(() => {
    if (lastSettled.current === settled) return;
    lastSettled.current = settled;
    const next = settled.trim();
    if (next === lastWritten.current) return;
    lastWritten.current = next;
    onWrite(next === '' ? undefined : next);
  }, [settled, onWrite]);

  useEffect(() => {
    if (value === lastWritten.current) return;
    lastWritten.current = value;
    setText(value);
  }, [value]);

  return (
    <input
      data-testid={ids.retrosSearchBox}
      type="search"
      className={touch ? 'field h-10 w-full' : 'field max-w-[340px] min-w-[200px] flex-[1_1_280px]'}
      enterKeyHint={touch ? 'search' : undefined}
      onKeyDown={(event) => {
        if (touch && event.key === 'Enter') event.currentTarget.blur();
      }}
      placeholder="Search points, root causes and raw logs"
      aria-label="Search retrospectives"
      value={text}
      onChange={(event) => setText(event.target.value)}
    />
  );
}
