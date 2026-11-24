import type { PromptDto } from '@shiplean/core/api/prompts';
import { promptsTestIds as ids } from '../testIds';
import { promptDiffHunks } from './diffParse';
import { PatchView } from './update/PatchView';

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
      className="grid"
    >
      {hunks.length === 0 ? (
        <p data-testid={ids.yourChangesEmpty} className="m-0 p-3 text-ink-muted">
          No changes
        </p>
      ) : (
        <div>
          <div
            data-testid={ids.diffKey}
            className="flex flex-wrap items-center gap-3 border-b border-dashed border-edge px-3 py-2 text-[length:var(--text-xs)] text-ink-muted"
          >
            <span className="inline-flex items-center gap-1">
              <i
                data-testid={ids.diffSwatchRemoved}
                aria-hidden="true"
                className="inline-block size-2.5 rounded-[2px] bg-[color-mix(in_srgb,var(--color-danger)_30%,transparent)]"
              />
              Built-in{prompt.custom ? ` (${prompt.custom.baseLabel})` : ''}
            </span>
            <span className="inline-flex items-center gap-1">
              <i
                data-testid={ids.diffSwatchAdded}
                aria-hidden="true"
                className="inline-block size-2.5 rounded-[2px] bg-[color-mix(in_srgb,var(--color-ok)_30%,transparent)]"
              />
              Custom v{prompt.custom?.number}
            </span>
            <span data-testid={ids.diffCounts} className="ml-auto">
              {added} added · {removed} removed
            </span>
          </div>
          <PatchView patch={patch ?? ''} testId={ids.yourChangesDiff} />
        </div>
      )}
    </section>
  );
}
