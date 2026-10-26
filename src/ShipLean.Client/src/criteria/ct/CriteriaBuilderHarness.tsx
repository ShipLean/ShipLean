import { useState } from 'react';
import type { CriteriaValidationError } from '../../api/criteria';
import type { CriteriaJson, FieldId } from '../../api/persisted';
import type { PickLists } from '../valueOptions';
import { CriteriaBuilder } from '../CriteriaBuilder';
import { criteriaHarnessTestIds } from './harnessIds';

/** CT-only: holds the value the controlled builder needs and prints the last value it emitted. */
export function CriteriaBuilderHarness({
  initial,
  errors,
  fields,
  showChips,
  replaceWith,
  ...lists
}: PickLists & {
  initial: CriteriaJson;
  errors?: CriteriaValidationError[];
  fields?: readonly FieldId[];
  showChips?: boolean;
  /** A value a button in the harness swaps in, the way a caller resets or clears the criteria. */
  replaceWith?: CriteriaJson;
}) {
  const [value, setValue] = useState(initial);
  return (
    <>
      <CriteriaBuilder
        value={value}
        onChange={setValue}
        errors={errors}
        fields={fields}
        showChips={showChips}
        {...lists}
      />
      {replaceWith ? (
        <button
          type="button"
          data-testid={criteriaHarnessTestIds.ctReplace}
          onClick={() => setValue(replaceWith)}
        >
          Replace
        </button>
      ) : null}
      <pre data-testid={criteriaHarnessTestIds.ctEmitted}>{JSON.stringify(value)}</pre>
    </>
  );
}
