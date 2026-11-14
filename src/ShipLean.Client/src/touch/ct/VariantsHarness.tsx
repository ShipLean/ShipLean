import { harnessTestIds } from './harnessIds';

/**
 * CT-only: one probe per K1 variant (bold only when its variant applies) and inputs styled the ways the app styles
 * them (.field, a Tailwind text size, an inline font size), for index.css's touch input rule to override.
 */
export function VariantsHarness() {
  return (
    <div>
      <p data-testid={harnessTestIds.ctTouchProbe} className="font-normal touch:font-bold">
        touch
      </p>
      <p data-testid={harnessTestIds.ctPhoneProbe} className="font-normal phone:font-bold">
        phone
      </p>
      <input data-testid={harnessTestIds.ctFieldInput} className="field" aria-label="Field" />
      <input
        data-testid={harnessTestIds.ctUtilityInput}
        className="text-[length:var(--text-sm)]"
        aria-label="Utility"
      />
      <input
        data-testid={harnessTestIds.ctInlineInput}
        style={{ fontSize: 12 }}
        aria-label="Inline"
      />
      <textarea data-testid={harnessTestIds.ctTextarea} className="field" aria-label="Notes" />
      <input data-testid={harnessTestIds.ctCheckbox} type="checkbox" aria-label="Check" />
      <input data-testid={harnessTestIds.ctRadio} type="radio" aria-label="Radio" />
      <select data-testid={harnessTestIds.ctSelect} className="field" aria-label="Pick">
        <option>One</option>
      </select>
    </div>
  );
}
