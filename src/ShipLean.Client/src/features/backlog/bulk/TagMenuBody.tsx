import { useState } from 'react';
import { inputClass } from '../../workItem/form/inputClasses';
import { backlogTestIds as ids } from '../testIds';

/** A text field and Add: the tag joins each selected row's own tags. */
export function TagMenuBody({ onAdd }: { onAdd: (tag: string) => void }) {
  const [text, setText] = useState('');
  const tag = text.trim();
  return (
    <form
      className="flex gap-1 p-1"
      onSubmit={(event) => {
        event.preventDefault();
        if (tag !== '') onAdd(tag);
      }}
    >
      <input
        autoFocus
        data-testid={ids.bulkTagInput}
        aria-label="Tag name"
        placeholder="Tag name"
        className={`${inputClass} w-32`}
        value={text}
        onChange={(event) => setText(event.target.value)}
      />
      <button
        type="submit"
        data-testid={ids.bulkMenuOption}
        disabled={tag === ''}
        className="btn-ghost disabled:opacity-50"
      >
        Add
      </button>
    </form>
  );
}
