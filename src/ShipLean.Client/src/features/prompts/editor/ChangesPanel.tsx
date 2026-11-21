import { Fragment } from 'react';
import type { PromptDiffHunk } from '../detail/diffParse';
import { promptsTestIds as ids } from '../testIds';

const marks = { add: '+', del: '−', context: ' ' } as const;
const tones = {
  add: 'bg-signal-soft',
  del: 'bg-alert/10 text-alert',
  context: 'text-ink-muted',
} as const;

// The editor's "Changes from built-in" tab: the unified diff of the text being edited against the built-in it started from.
export function ChangesPanel({ hunks }: { hunks: PromptDiffHunk[] }) {
  if (hunks.length === 0) {
    return (
      <p data-testid={ids.editorChanges} className="m-0 p-3 text-ink-muted">
        The text is the same as the built-in.
      </p>
    );
  }
  return (
    <div
      data-testid={ids.editorChanges}
      className="overflow-x-auto font-[family-name:var(--font-mono)] text-[length:var(--text-sm)]"
    >
      {hunks.map((hunk, h) => (
        <Fragment key={h}>
          {hunk.skippedBefore > 0 ? (
            <div data-testid={ids.diffGap} className="bg-sunken px-3 py-0.5 text-ink-muted">
              {hunk.skippedBefore} unchanged {hunk.skippedBefore === 1 ? 'line' : 'lines'}
            </div>
          ) : null}
          {hunk.lines.map((line, i) => (
            <div
              key={i}
              data-testid={ids.diffLine}
              data-kind={line.kind}
              className={`flex gap-2 whitespace-pre px-3 ${tones[line.kind]}`}
            >
              <span aria-hidden>{marks[line.kind]}</span>
              <span>{line.text}</span>
            </div>
          ))}
        </Fragment>
      ))}
    </div>
  );
}
