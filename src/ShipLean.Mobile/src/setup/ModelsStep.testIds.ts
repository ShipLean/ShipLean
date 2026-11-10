export const modelsStepIds = {
  root: 'setup-models',
  // The three command model choices.
  optionAuto: 'setup-models-option-auto',
  option17: 'setup-models-option-qwen3-1.7b',
  option4: 'setup-models-option-qwen3-4b',
  // What Auto picked for this phone: "This phone: 12 GB → Qwen3 4B".
  autoPick: 'setup-models-auto-pick',
  // The 8 GB warning on 4B, and the reason 4B can't be picked on 6 GB.
  warning4: 'setup-models-warning-4b',
  blocked4: 'setup-models-blocked-4b',
  speech: 'setup-models-speech',
  total: 'setup-models-total',
  free: 'setup-models-free',
  freeBar: 'setup-models-free-bar',
  wifiOnly: 'setup-models-wifi-only',
  estimateNote: 'setup-models-estimate-note',
  download: 'setup-models-download',
  skip: 'setup-models-skip',
  // A phone with too little memory: the reason and Continue.
  noVoice: 'setup-models-no-voice',
  noVoiceContinue: 'setup-models-no-voice-continue',
} as const;

export const modelOptionId = (id: 'auto' | 'qwen3-1.7b' | 'qwen3-4b') =>
  id === 'auto'
    ? modelsStepIds.optionAuto
    : id === 'qwen3-1.7b'
      ? modelsStepIds.option17
      : modelsStepIds.option4;
