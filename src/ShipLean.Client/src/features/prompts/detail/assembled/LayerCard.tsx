import type { PromptDto, PromptLayerDto, PromptStatus } from '@shiplean/core/api/prompts';
import { promptsTestIds as ids } from '../../testIds';
import { Chip, type ChipTone } from '../../../../components/Chip';
import { isOwnedLayer, layerNumber } from './layerNumber';

const statusLabels: Record<PromptStatus, { tone: ChipTone; label: (p: PromptDto) => string }> = {
  builtIn: { tone: 'muted', label: () => 'Built-in' },
  custom: { tone: 'purple', label: (p) => `Custom v${p.custom?.number}` },
  builtInUpdated: { tone: 'warn', label: () => 'Built-in updated' },
  usingBuiltIn: { tone: 'muted', label: () => 'Using built-in' },
};

// Planned layers say Planned, the layers this prompt owns carry its status chip, the rest are Fixed.
function chipOf(layer: PromptLayerDto, prompt: PromptDto, owned: boolean) {
  const status = statusLabels[prompt.status];
  return (
    <span data-testid={ids.assembledLayerChip} className="inline-flex">
      {layer.planned ? (
        <Chip>Planned</Chip>
      ) : owned ? (
        <Chip tone={status.tone}>{status.label(prompt)}</Chip>
      ) : (
        <Chip>Fixed</Chip>
      )}
    </span>
  );
}

// One layer of what the agent gets: a numbered circle, title, one-line note and a chip.
export function LayerCard(props: { layer: PromptLayerDto; prompt: PromptDto }) {
  const { layer, prompt } = props;
  const owned = !layer.planned && isOwnedLayer(layer.kind);
  const testId = layer.planned
    ? ids.assembledLayerPlanned
    : layer.kind === 'prompt'
      ? ids.assembledLayerThis
      : ids.assembledLayer;
  const border = layer.planned
    ? 'border-dashed border-edge'
    : owned
      ? 'border-signal shadow-[inset_0_0_0_1px_var(--color-signal)]'
      : 'border-edge';
  return (
    <li
      data-testid={testId}
      data-kind={layer.kind}
      data-owned={owned}
      className={`grid grid-cols-[26px_minmax(0,1fr)_auto] items-center gap-2 rounded-control border bg-panel px-2.5 py-2 text-[length:var(--text-sm)] ${border}`}
    >
      <span
        data-testid={ids.assembledLayerNumber}
        aria-hidden="true"
        className={`grid size-[22px] place-items-center rounded-full text-[length:var(--text-2xs)] font-bold ${
          owned ? 'bg-signal text-signal-ink' : 'bg-sunken text-ink-muted'
        }`}
      >
        {layerNumber(layer.kind)}
      </span>
      <span className="grid min-w-0">
        <strong>{layer.title}</strong>
        {layer.note === null ? null : (
          <span className="text-[length:var(--text-xs)] text-ink-muted">{layer.note}</span>
        )}
      </span>
      {chipOf(layer, prompt, owned)}
    </li>
  );
}
