import { useId } from 'react';
import { workItemTestIds as ids } from '../testIds';
import { FieldCell } from './FieldCell';
import { inputClass } from './inputClasses';

export function PointsField({
  value,
  changed,
  onChange,
}: {
  value: number | null;
  changed: boolean;
  onChange: (points: number | null) => void;
}) {
  const inputId = useId();
  return (
    <FieldCell label="Story points" field="points" changed={changed} htmlFor={inputId}>
      <input
        id={inputId}
        data-testid={ids.points}
        type="number"
        min={0}
        step={1}
        inputMode="numeric"
        placeholder="?"
        title={value === null ? 'Not estimated: counts as 0 in the forecast' : undefined}
        className={`${inputClass} w-20`}
        value={value ?? ''}
        onChange={(event) => {
          const text = event.target.value;
          onChange(text === '' ? null : Number(text));
        }}
      />
    </FieldCell>
  );
}
