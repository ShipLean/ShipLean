import type { PromptLayerKind } from '@shiplean/core/api/prompts';

// The mock-up's numbering: the prompt itself is 2 and its agent sections 2a, so the layers after them keep 3 and +.
const numbers: Record<PromptLayerKind, string> = {
  serverInstructions: '1',
  prompt: '2',
  agentSections: '2a',
  waysOfWorking: '3',
  clientNotes: '+',
};

export function layerNumber(kind: PromptLayerKind): string {
  return numbers[kind];
}

// The layers a custom version replaces.
export function isOwnedLayer(kind: PromptLayerKind): boolean {
  return kind === 'prompt' || kind === 'agentSections';
}
