import { useState } from 'react';
import { Chip } from '../../../components/Chip';
import { workItemTestIds as ids } from '../testIds';
import { FieldCell } from './FieldCell';

export function TagsField({
  value,
  changed,
  onChange,
}: {
  value: string[];
  changed: boolean;
  onChange: (tags: string[]) => void;
}) {
  const [text, setText] = useState('');
  const add = () => {
    const tag = text.trim();
    setText('');
    if (tag !== '' && !value.includes(tag)) onChange([...value, tag]);
  };
  return (
    <FieldCell
      label="Tags"
      field="tags"
      changed={changed}
      htmlFor="work-item-tag-input"
      className="grow"
    >
      <div data-testid={ids.tags} className="flex min-h-7 flex-wrap items-center gap-1">
        {value.map((tag) => (
          <Chip key={tag} onRemove={() => onChange(value.filter((t) => t !== tag))}>
            {tag}
          </Chip>
        ))}
        <input
          id="work-item-tag-input"
          data-testid={ids.tagInput}
          placeholder="+ Add tag"
          autoComplete="off"
          className="h-7 w-24 bg-transparent px-1 text-[length:var(--text-sm)] text-ink placeholder:text-ink-muted focus-visible:outline-2 focus-visible:outline-signal"
          value={text}
          onChange={(event) => setText(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === 'Enter') {
              event.preventDefault();
              add();
            }
          }}
          onBlur={add}
        />
      </div>
    </FieldCell>
  );
}
