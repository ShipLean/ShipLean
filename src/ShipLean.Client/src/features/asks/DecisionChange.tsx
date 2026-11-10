import { useId, useState } from 'react';
import { showToast } from '../../components/toastStore';
import { useMe } from '../shell/useShellQueries';
import { inputClass, textareaClass } from '../workItem/form/inputClasses';
import type { DecisionChange as ChangeBody } from './asksApi';
import type { DecisionEditorProps } from './regions';
import { asksTestIds as ids } from './testIds';
import { useAskWrites } from './useAskWrites';

type Choice = 'recommended' | 'current' | 'own' | `alternative:${number}`;

interface Option {
  value: Choice;
  text: string;
  tagline: string;
}

const optionClass =
  'grid cursor-pointer grid-cols-[16px_minmax(0,1fr)_auto] items-start gap-2 rounded-control border border-edge bg-panel px-2 py-1.5 text-[length:var(--text-sm)] has-[:checked]:border-signal has-[:checked]:bg-signal-soft';
const taglineClass = 'whitespace-nowrap pt-0.5 text-[length:var(--text-2xs)] text-ink-muted';

// The inline change form under a decision: the recommended answer, an alternative, the current answer (when it is the
// owner's own words) or a new own answer, with an optional note. It saves straight away, not with the item's Save.
export function DecisionChange({ item, decision, onClose }: DecisionEditorProps) {
  const groupName = useId();
  const me = useMe().data;
  const { change, confirm } = useAskWrites(item.projectId, item.key, item.id);
  const settled = decision.state === 'confirmed' || decision.state === 'changed';
  const open = decision.state === 'open';
  const current = decision.currentAnswer;

  const options: Option[] = [
    { value: 'recommended', text: decision.recommended, tagline: 'Recommended' },
    ...decision.alternatives.map((text, index): Option => ({
      value: `alternative:${index}`,
      text,
      tagline: 'Alternative',
    })),
    ...(decision.answerKind === 'own'
      ? [{ value: 'current', text: current, tagline: 'Current answer' } satisfies Option]
      : []),
  ];
  const initial: Choice =
    decision.answerKind === 'own'
      ? 'current'
      : decision.answerKind === 'alternative' && decision.alternativeIndex !== null
        ? `alternative:${decision.alternativeIndex}`
        : 'recommended';

  const [pick, setPick] = useState<Choice>(initial);
  const [ownText, setOwnText] = useState('');
  const [note, setNote] = useState('');
  const own = ownText.trim();
  const pending = change.isPending || confirm.isPending;
  const error = change.error ?? confirm.error;

  const textOf = (value: Choice): string =>
    value === 'own' ? own : (options.find((option) => option.value === value)?.text ?? '');

  const save = () => {
    if (pending || (pick === 'own' && own === '')) return;
    const text = textOf(pick);
    if (text === current && !open) {
      onClose();
      showToast(`${decision.label} unchanged`);
      return;
    }
    const trimmedNote = note.trim() === '' ? null : note.trim();
    if (pick === 'recommended' && open && trimmedNote === null) {
      confirm.mutate([decision.number], {
        onSuccess: () => {
          showToast(`${decision.label} confirmed`);
          onClose();
        },
      });
      return;
    }
    const body: ChangeBody =
      pick === 'recommended'
        ? { kind: 'recommended', alternativeIndex: null, text: null, note: trimmedNote }
        : pick === 'own'
          ? { kind: 'own', alternativeIndex: null, text: own, note: trimmedNote }
          : {
              kind: 'alternative',
              alternativeIndex: Number(pick.slice('alternative:'.length)),
              text: null,
              note: trimmedNote,
            };
    change.mutate(
      { number: decision.number, change: body },
      {
        onSuccess: () => {
          showToast(
            settled
              ? `${decision.label} changed · comment posted for the next agent`
              : pick === 'recommended' && open
                ? `${decision.label} confirmed`
                : `${decision.label} changed`,
          );
          onClose();
        },
      },
    );
  };

  return (
    <div
      data-testid={ids.decisionChange}
      className="mt-1.5 grid gap-1.5 rounded-panel border border-signal bg-canvas px-3 py-2"
    >
      <span className="text-[length:var(--text-xs)] font-medium text-ink-muted">
        Change {decision.label}: pick an answer or write your own
      </span>
      <div role="radiogroup" aria-label={`Answer for ${decision.label}`} className="grid gap-1">
        {options.map((option) => (
          <label key={option.value} className={optionClass}>
            <input
              type="radio"
              name={groupName}
              data-testid={ids.changeOption}
              className="mt-[3px] accent-signal"
              checked={pick === option.value}
              onChange={() => setPick(option.value)}
            />
            <span>{option.text}</span>
            <span className={taglineClass}>
              {option.tagline}
              {option.text === current ? ' · current' : ''}
            </span>
          </label>
        ))}
        <label className={optionClass}>
          <input
            type="radio"
            name={groupName}
            data-testid={ids.changeOption}
            className="mt-[3px] accent-signal"
            checked={pick === 'own'}
            onChange={() => setPick('own')}
          />
          <span className="grid gap-1">
            Your own answer
            <textarea
              data-testid={ids.changeOwnInput}
              aria-label={`Your own answer for ${decision.label}`}
              rows={2}
              placeholder="Write the answer the next agent should follow"
              className={`${textareaClass} min-h-[52px] font-[family-name:var(--font-sans)]`}
              value={ownText}
              onFocus={() => setPick('own')}
              onChange={(event) => {
                setOwnText(event.target.value);
                setPick('own');
              }}
            />
          </span>
          <span className={taglineClass}>Your words</span>
        </label>
      </div>
      <label
        htmlFor={`${groupName}-note`}
        className="text-[length:var(--text-xs)] font-medium text-ink-muted"
      >
        Note (optional): why
      </label>
      <input
        id={`${groupName}-note`}
        data-testid={ids.changeNote}
        autoComplete="off"
        placeholder="Shown in the history and in the comment"
        className={`${inputClass} w-full`}
        value={note}
        onChange={(event) => setNote(event.target.value)}
      />
      {settled ? (
        <div
          data-testid={ids.changeSettledWarning}
          className="rounded-control border border-caution bg-[color-mix(in_srgb,var(--color-warn)_10%,var(--color-surface))] px-2 py-1.5 text-[length:var(--text-sm)]"
        >
          <b className="mr-1">
            {decision.label} is already {decision.state}.
          </b>
          Saving a different answer posts a comment on {item.key} (“{decision.label} changed from …
          to … by {me?.name ?? 'you'}”) so the next agent picks it up. The old answer stays in the
          history.
        </div>
      ) : null}
      <div className="flex flex-wrap items-center gap-2">
        <button
          type="button"
          data-testid={ids.changeSave}
          disabled={pending || (pick === 'own' && own === '')}
          className="h-7 rounded-control bg-signal px-3 text-[length:var(--text-sm)] font-semibold text-signal-ink disabled:opacity-50"
          onClick={save}
        >
          Save answer
        </button>
        <button
          type="button"
          data-testid={ids.changeCancel}
          className="h-7 rounded-control px-3 text-[length:var(--text-sm)] text-ink hover:bg-signal-soft"
          onClick={() => {
            change.reset();
            confirm.reset();
            onClose();
          }}
        >
          Cancel
        </button>
        <span className="ml-auto text-[length:var(--text-xs)] text-ink-muted">
          Saves straight away, not with the item’s Save
        </span>
      </div>
      {error ? (
        <span
          data-testid={ids.changeError}
          role="alert"
          className="text-[length:var(--text-xs)] text-alert"
        >
          {error.message}
        </span>
      ) : null}
    </div>
  );
}
