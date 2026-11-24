import { useState } from 'react';
import type { PromptDto } from '@shiplean/core/api/prompts';
import { promptsTestIds as ids } from '../testIds';
import { useKeepMine, useSavePromptVersion } from '../usePrompts';
import { PatchView } from './update/PatchView';
import { errorText } from './update/errorText';
import { Chip } from '../../../components/Chip';
import { Code } from './update/Code';

// The built-in's own change, with Applies cleanly and the apply actions.
export function UpdateTab(props: {
  projectId: string;
  prompt: PromptDto;
  canEdit: boolean;
  onApplyAndEdit: () => void;
}) {
  const { prompt, projectId } = props;
  const save = useSavePromptVersion(projectId, prompt.key);
  const keep = useKeepMine(projectId, prompt.key);
  const [applyError, setApplyError] = useState<string | null>(null);
  const [keepError, setKeepError] = useState<string | null>(null);
  const update = prompt.update;
  if (update === null) {
    return (
      <section
        data-testid={ids.panelUpdate}
        aria-label={`Built-in update of ${prompt.name}`}
        data-can-edit={props.canEdit}
      />
    );
  }
  const busy = save.isPending || keep.isPending;
  const apply = () => {
    setApplyError(null);
    save.mutate(
      {
        text: update.mergedText,
        note: `Applied the ${update.changedLabel} built-in change`,
        source: 'builtInUpdate',
      },
      { onError: (e) => setApplyError(errorText(e, 'The change could not be applied.')) },
    );
  };
  const keepMine = () => {
    setKeepError(null);
    keep.mutate(undefined, {
      onError: (e) => setKeepError(errorText(e, 'Your version could not be kept.')),
    });
  };
  return (
    <section
      data-testid={ids.panelUpdate}
      aria-label={`Built-in update of ${prompt.name}`}
      data-can-edit={props.canEdit}
      className="grid gap-3 p-3"
    >
      <div
        role="note"
        data-testid={ids.updateExplainer}
        className="rounded-control border border-edge border-l-[3px] border-l-caution bg-[color-mix(in_srgb,var(--color-caution)_9%,var(--color-panel))] px-3 py-2 text-[length:var(--text-sm)]"
      >
        ShipLean shipped a new built-in <Code>{prompt.file}</Code> on {update.changedLabel} (
        <Code>{prompt.builtInHash.slice(0, 8)}</Code>). Below is only what changed in the built-in.{' '}
        {update.appliesCleanly
          ? 'It doesn\u2019t touch the lines you changed, so it applies cleanly to your version.'
          : 'It touches lines you changed, so apply it and edit to resolve them.'}
      </div>
      <div className="overflow-hidden rounded-control border border-edge bg-panel">
        <div
          data-testid={ids.updateDiffHeader}
          className="flex flex-wrap items-center gap-2 border-b border-edge bg-sunken px-3 py-1.5 text-[length:var(--text-sm)]"
        >
          <strong data-testid={ids.updateBaseNote}>What changed in the built-in</strong>
          <span className="text-ink-muted">
            {update.baseLabel} {'\u2192'} {update.changedLabel}
          </span>
          <span className="flex-1" />
          {update.appliesCleanly ? (
            <span data-testid={ids.updateAppliesCleanly} className="inline-flex">
              <Chip tone="ok">Applies cleanly</Chip>
            </span>
          ) : (
            <span data-testid={ids.updateNeedsMerge} className="text-caution">
              Conflicts with your changes: Apply and edit to resolve them
            </span>
          )}
        </div>
        <PatchView patch={update.patch} testId={ids.updateDiff} />
      </div>
      {props.canEdit ? (
        <div className="flex flex-wrap items-center gap-2">
          {update.appliesCleanly ? (
            <button
              type="button"
              data-testid={ids.updateApply}
              className="btn-signal touch:min-h-[var(--touch-target)]"
              disabled={busy}
              onClick={apply}
            >
              Apply to my version{prompt.custom ? ` (makes v${prompt.custom.number + 1})` : ''}
            </button>
          ) : null}
          <button
            type="button"
            data-testid={ids.updateApplyEdit}
            className={
              update.appliesCleanly
                ? 'btn touch:min-h-[var(--touch-target)]'
                : 'btn-signal touch:min-h-[var(--touch-target)]'
            }
            disabled={busy}
            onClick={props.onApplyAndEdit}
          >
            Apply and edit
          </button>
          <button
            type="button"
            data-testid={ids.updateKeep}
            className="btn touch:min-h-[var(--touch-target)]"
            disabled={busy}
            onClick={keepMine}
          >
            Keep mine as it is
          </button>
          <span className="text-[length:var(--text-xs)] text-ink-muted">
            Keep mine hides this until the built-in changes again.
          </span>
        </div>
      ) : null}
      {applyError === null ? null : (
        <p role="alert" data-testid={ids.updateApplyError} className="m-0 text-alert">
          {applyError}
        </p>
      )}
      {keepError === null ? null : (
        <p role="alert" data-testid={ids.updateKeepError} className="m-0 text-alert">
          {keepError}
        </p>
      )}
    </section>
  );
}
