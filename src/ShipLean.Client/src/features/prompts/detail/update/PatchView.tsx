import { promptsTestIds as ids } from '../../testIds';
import { promptDiffHunks, type PromptDiffLineKind } from '../diffParse';

const sign: Record<PromptDiffLineKind, string> = { context: ' ', add: '+', del: '-' };
const signClass: Record<PromptDiffLineKind, string> = {
  context: 'text-ink-muted',
  add: 'text-good',
  del: 'text-alert',
};
const rowClass: Record<PromptDiffLineKind, string> = {
  context: '',
  add: 'bg-[color-mix(in_srgb,var(--color-ok)_14%,transparent)]',
  del: 'bg-[color-mix(in_srgb,var(--color-danger)_12%,transparent)]',
};

// Old line, new line, sign, code: the gap row uses the same columns so its text lines up with the code (.diff in the
// mock-up).
const columns = 'grid grid-cols-[2.5rem_2.5rem_1rem_minmax(0,1fr)]';

// A header-less patch drawn as hunks, the way the Your changes tab draws the custom version's.
export function PatchView(props: { patch: string; testId: string }) {
  const hunks = props.patch === '' ? [] : promptDiffHunks(props.patch);
  return (
    <div
      data-testid={props.testId}
      className="py-2 font-[family-name:var(--font-mono)] text-[length:var(--text-sm)] leading-normal"
    >
      {hunks.map((hunk, h) => (
        <div key={h}>
          {hunk.skippedBefore > 0 ? (
            <div
              data-testid={ids.diffGap}
              className={`${columns} my-0.5 bg-sunken py-0.5 font-sans text-[length:var(--text-xs)] text-ink-muted`}
            >
              <span data-testid={ids.diffGapText} className="col-start-4">
                {hunk.skippedBefore} unchanged lines
              </span>
            </div>
          ) : null}
          {hunk.lines.map((line, i) => (
            <div
              key={i}
              data-testid={ids.diffLine}
              data-kind={line.kind}
              className={`${columns} whitespace-pre-wrap [overflow-wrap:anywhere] ${rowClass[line.kind]}`}
            >
              <span
                aria-hidden="true"
                className="select-none pr-1.5 text-right text-[length:var(--text-xs)] text-ink-muted"
              >
                {line.oldLine}
              </span>
              <span
                aria-hidden="true"
                className="select-none pr-1.5 text-right text-[length:var(--text-xs)] text-ink-muted"
              >
                {line.newLine}
              </span>
              <span
                aria-hidden="true"
                data-testid={ids.diffSign}
                className={`select-none text-center ${signClass[line.kind]}`}
              >
                {sign[line.kind]}
              </span>
              <span>{line.text}</span>
            </div>
          ))}
        </div>
      ))}
    </div>
  );
}
