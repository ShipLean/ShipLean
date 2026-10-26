import type { KeyCheckDto, ProjectSummaryDto } from '../../../api/projects';
import { settingsTestIds as ids } from '../testIds';
import type { FormProps } from './formProps';
import { keyHasLength, keyHasOnlyLetters, suggestKey } from './model';

interface KeyFieldProps extends FormProps {
  check: KeyCheckDto | undefined;
  pending: boolean;
  sources: ProjectSummaryDto[];
  shownKey: string;
  /** The suggestion that was taken, when the shown key is the alternative that replaced it. */
  skippedFrom: string | undefined;
}

// A key taken by one of your own projects names it; any other project stays unnamed.
function takenText(key: string, check: KeyCheckDto, sources: ProjectSummaryDto[]) {
  if (check.reason !== 'taken') return `${key} is used by another project`;
  const own = sources.find((p) => p.key === key);
  return own === undefined
    ? `${key} is used by a project you are in`
    : `${key} is used by ${own.name}`;
}

type Mark = boolean | 'pending' | 'unknown';

const MARKS = { true: '✓', false: '✕', pending: '…', unknown: '·' } as const;

function Check({ ok, testId, children }: { ok: Mark; testId: string; children: string }) {
  const colour = ok === false ? 'text-alert' : 'text-ink-muted';
  return (
    <li data-testid={testId} data-ok={ok} className={colour}>
      {MARKS[`${ok}`]} {children}
    </li>
  );
}

export function KeyField({
  form,
  patch,
  check,
  pending,
  sources,
  shownKey: key,
  skippedFrom,
}: KeyFieldProps) {
  const suggestion = suggestKey(form.name);
  return (
    <div className="grid gap-1">
      <div className="flex items-center gap-2">
        <input
          id="settings-newproject-key"
          data-testid={ids.newProjectKey}
          aria-label="Key"
          aria-describedby="settings-newproject-key-checks"
          maxLength={6}
          spellCheck={false}
          autoComplete="off"
          className="h-7 w-24 rounded-control border border-edge bg-panel px-2 font-mono text-[length:var(--text-sm)] uppercase text-ink"
          value={key}
          onChange={(e) =>
            patch({ keyEdited: true, typedKey: e.target.value.toUpperCase().trim() })
          }
        />
        {suggestion !== '' && key !== suggestion && skippedFrom === undefined ? (
          <button
            type="button"
            data-testid={ids.newProjectKeySuggest}
            className="h-7 rounded-control px-2 text-[length:var(--text-sm)] text-signal"
            onClick={() => patch({ keyEdited: false, typedKey: '' })}
          >
            Use suggestion {suggestion}
          </button>
        ) : null}
      </div>
      {skippedFrom === undefined ? null : (
        <div className="text-[length:var(--text-sm)] text-ink-muted">
          {skippedFrom} is taken, so {key} is suggested.
        </div>
      )}
      <ul id="settings-newproject-key-checks" className="grid gap-0.5 text-[length:var(--text-sm)]">
        <Check ok={keyHasLength(key)} testId={ids.newProjectCheckLength}>
          2 to 4 characters
        </Check>
        <Check ok={keyHasOnlyLetters(key)} testId={ids.newProjectCheckLetters}>
          Letters A to Z only, no digits
        </Check>
        {check !== undefined && !check.available ? (
          <Check ok={false} testId={ids.newProjectCheckFree}>
            {takenText(key, check, sources)}
          </Check>
        ) : (
          <Check
            ok={check === undefined ? (pending ? 'pending' : 'unknown') : true}
            testId={ids.newProjectCheckFree}
          >
            {pending ? 'Checking…' : 'Not used by another project'}
          </Check>
        )}
      </ul>
      {check !== undefined && check.alternatives.length > 0 ? (
        <div className="flex items-center gap-2 text-[length:var(--text-sm)] text-ink-muted">
          Other free keys
          {check.alternatives.map((alternative) => (
            <button
              key={alternative}
              type="button"
              data-testid={ids.newProjectAlternative}
              className="h-6 rounded-control border border-edge bg-panel px-2 font-mono text-ink"
              onClick={() => patch({ keyEdited: true, typedKey: alternative })}
            >
              {alternative}
            </button>
          ))}
        </div>
      ) : null}
      <div className="text-[length:var(--text-sm)] text-ink-muted">
        The key is permanent: it is part of every work item key and every link to one.
      </div>
    </div>
  );
}
