import { backlogTestIds as ids } from '../testIds';
import type { SlugCheck } from './newFeatureApi';

// The Slug input and what the check said about it. `serverError` is a slug_taken from Create itself (the check can race a create).
export function SlugField({
  value,
  onChange,
  check,
  serverError,
}: {
  value: string;
  onChange: (slug: string) => void;
  // The answer for exactly `value`, or undefined while it is being asked.
  check: SlugCheck | undefined;
  serverError: string | null;
}) {
  const problem = serverError ?? (check && !check.available ? slugProblem(check) : null);
  return (
    <>
      <input
        id="new-feature-slug"
        data-testid={ids.newFeatureSlug}
        className="field w-full font-[family-name:var(--font-mono)]"
        value={value}
        spellCheck={false}
        aria-invalid={problem !== null}
        onChange={(event) => onChange(event.target.value)}
      />
      {problem ? (
        <p
          data-testid={ids.newFeatureSlugNote}
          data-available="false"
          role="alert"
          className="m-0 text-[length:var(--text-sm)] text-alert"
        >
          {problem}
        </p>
      ) : check?.available ? (
        <p
          data-testid={ids.newFeatureSlugNote}
          data-available="true"
          className="m-0 text-[length:var(--text-sm)] text-good"
        >
          ✓ Free. Agents and imports find this Feature by it.
        </p>
      ) : null}
    </>
  );
}

const slugProblem = (check: SlugCheck) =>
  check.reason ?? 'Another Feature in this project already uses it.';
