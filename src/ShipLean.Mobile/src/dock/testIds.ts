// The dock's composer (src/dock). The closed dock keeps primitiveIds.dock, dockField and dockMic (ui/testIds.ts).
export const dockIds = {
  // The closed field, pressed to open the composer.
  open: 'dock-open',
  // The draft's mode label on the closed dock ("Note draft").
  draftLabel: 'dock-draft-label',
  composer: 'dock-composer',
  // The Note | Command switch: one id per option.
  modeNote: 'dock-mode-note',
  modeCommand: 'dock-mode-command',
  // Where the text goes ("on SHIP-906").
  target: 'dock-target',
  // Why Command is greyed.
  commandOff: 'dock-command-off',
  noteField: 'dock-note-field',
  route: 'dock-route',
  post: 'dock-post',
  nudge: 'dock-nudge',
  nudgeTitle: 'dock-nudge-title',
  nudgePreview: 'dock-nudge-preview',
  mentions: 'dock-mentions',
} as const;

export const mentionOptionId = (userId: string) => `dock-mention-${userId}`;
