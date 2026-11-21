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
          <PatchView patch={patch ?? ''} testId={ids.yourChangesDiff} />
        </div>
      )}
    </section>
  );
}
