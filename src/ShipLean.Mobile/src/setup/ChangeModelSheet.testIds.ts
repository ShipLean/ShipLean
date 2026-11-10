export const changeModelIds = {
  root: 'sheet-change-model',
  summary: 'sheet-change-model-summary',
  cancel: 'sheet-change-model-cancel',
  download: 'sheet-change-model-download',
} as const;

export const changeModelOptionId = (id: 'auto' | 'qwen3-1.7b' | 'qwen3-4b') =>
  `sheet-change-model-option-${id}`;
