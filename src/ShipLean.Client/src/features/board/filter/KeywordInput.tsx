import { useEffect, useState } from 'react';
import { boardTestIds as ids } from '../testIds';

const TYPING_PAUSE_MS = 250;

// The keyword box: the board is asked once typing pauses. The bar remounts it (by key) when the filter is cleared.
export function KeywordInput({
  value,
  onChange,
  className = 'field w-56',
  onDraft,
}: {
  value: string;
  onChange: (value: string) => void;
  className?: string;
  /** Hears every keystroke, before the pause: the Filter sheet applies the last one when it closes. */
  onDraft?: (text: string) => void;
}) {
  const [text, setText] = useState(value);
  useEffect(() => {
    if (text.trim() === value) return undefined;
    const timer = setTimeout(() => onChange(text.trim()), TYPING_PAUSE_MS);
    return () => clearTimeout(timer);
  }, [text, value, onChange]);
  return (
    <input
      type="search"
      data-testid={ids.filterKeyword}
      className={className}
      placeholder="Filter by keyword or key"
      aria-label="Filter by keyword"
      value={text}
      onChange={(event) => {
        setText(event.target.value);
        onDraft?.(event.target.value);
      }}
    />
  );
}
