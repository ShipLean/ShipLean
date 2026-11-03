import { useState } from 'react';
import { inputClass } from '../../workItem/form/inputClasses';
import { backlogTestIds as ids } from '../testIds';

/** A number field and Set: a number sets the estimate on every selected row, an empty field clears it. */
export function PointsMenuBody({ onSet }: { onSet: (points: number | null) => void }) {
  const [text, setText] = useState('');
  const trimmed = text.trim();
  const points = trimmed === '' ? null : Number(trimmed);
  const valid = points === null || (Number.isFinite(points) && points >= 0);
  return (
    <form
      className="flex gap-1 p-1"
      onSubmit={(event) => {
        event.preventDefault();
        if (valid) onSet(points);
      }}
    >
      <input
        autoFocus
        type="number"
        min={0}
        step="any"
        data-testid={ids.bulkPointsInput}
        aria-label="Points"
        placeholder="Empty clears"
        className={`${inputClass} w-28`}
        value={text}
        onChange={(event) => setText(event.target.value)}
      />
      <button
        type="submit"
        data-testid={ids.bulkMenuOption}
        disabled={!valid}
        className="btn-ghost disabled:opacity-50"
      >
        Set
      </button>
    </form>
  );
}
