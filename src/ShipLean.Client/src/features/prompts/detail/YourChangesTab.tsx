import type { PromptDto } from '@shiplean/core/api/prompts';
import { promptsTestIds as ids } from '../testIds';
import { promptDiffHunks, type PromptDiffLineKind } from './diffParse';

const sign: Record<PromptDiffLineKind, string> = { context: ' ', add: '+', del: '-' };
const rowClass: Record<PromptDiffLineKind, string> = {
  context: '',
  add: 'bg-[color-mix(in_srgb,var(--color-ok)_14%,transparent)]',
  del: 'bg-[color-mix(in_srgb,var(--color-danger)_12%,transparent)]',
};

// The diff of the custom version against the built-in it started from.
export function YourChangesTab(props: { prompt: PromptDto }) {
  const { prompt } = props;
  const patch = prompt.yourChangesPatch;
  const hunks = patch === null || patch === '' ? [] : promptDiffHunks(patch);
  const lines = hunks.flatMap((h) => h.lines);
  const added = lines.filter((l) => l.kind === 'add').length;
  const removed = lines.filter((l) => l.kind === 'del').length;
  return (
    <section
      data-testid={ids.panelYourChanges}
      aria-label={`Your changes to ${prompt.name}`}
      className="grid gap-2"
    >
      {hunks.length === 0 ? (
        <p data-testid={ids.yourChangesEmpty} className="m-0 text-ink-muted">
          No changes
        </p>
      ) : (
        <div className="rounded-control border border-edge">
          <div
            data-testid={ids.diffKey}
            className="flex flex-wrap gap-3 border-b border-dashed border-edge px-3 py-2 text-[length:var(--text-xs)] text-ink-muted"
          >
            <span>Built-in{prompt.custom ? ` (${prompt.custom.baseLabel})` : ''}</span>
            <span>Custom v{prompt.custom?.number}</span>
            <span>
              {added} added, {removed} removed
            </span>
          </div>
          <div
            data-testid={ids.yourChangesDiff}
            className="py-2 font-[family-name:var(--font-mono)] text-[length:var(--text-xs)] leading-normal"
          >
            {hunks.map((hunk, h) => (
              <div key={h}>
                {hunk.skippedBefore > 0 ? (
                  <div
                    data-testid={ids.diffGap}
                    className="bg-sunken px-3 py-0.5 font-sans text-ink-muted"
                  >
                    {hunk.skippedBefore} unchanged lines
                  </div>
                ) : null}
                {hunk.lines.map((line, i) => (
                  <div
                    key={i}
                    data-testid={ids.diffLine}
                    data-kind={line.kind}
                    className={`grid grid-cols-[2.5rem_2.5rem_1rem_minmax(0,1fr)] whitespace-pre-wrap [overflow-wrap:anywhere] ${rowClass[line.kind]}`}
                  >
                    <span aria-hidden="true" className="select-none pr-1 text-right text-ink-muted">
                      {line.oldLine}
                    </span>
                    <span aria-hidden="true" className="select-none pr-1 text-right text-ink-muted">
                      {line.newLine}
                    </span>
                    <span aria-hidden="true" className="select-none text-center">
                      {sign[line.kind]}
                    </span>
                    <span>{line.text}</span>
                  </div>
                ))}
              </div>
            ))}
          </div>
        </div>
      )}
    </section>
  );
}
