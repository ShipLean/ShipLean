import type { RetroFixType } from '@shiplean/core/api/retros';
import { retroFixOptions } from '../../retroShared/actionDialog/retroFixTypes';
import { retrosTestIds as ids } from '../testIds';

/** The action item's type of fix. The empty option is there only while the stored fix is null (a back-copied legacy action). */
export function WhyFixSelect({
  fixType,
  disabled,
  onChange,
}: {
  fixType: RetroFixType | null;
  disabled: boolean;
  onChange: (fixType: RetroFixType) => void;
}) {
  return (
    <label className="flex items-center gap-2 text-[length:var(--text-sm)]">
      <span className="text-ink-muted">Type of fix</span>
      <select
        data-testid={ids.retroActionWhyFix}
        disabled={disabled}
        value={fixType ?? ''}
        className="field"
        onChange={(e) => onChange(e.target.value as RetroFixType)}
      >
        {fixType === null ? <option value="" /> : null}
        {retroFixOptions.map((fix) => (
          <option key={fix.id} value={fix.id}>
            {fix.label}
          </option>
        ))}
      </select>
    </label>
  );
}
