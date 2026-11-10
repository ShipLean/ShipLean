// The line under the composer's field that says why it opened for typing. Editing a previewed command needs none.
import type { ComposerReason } from './composerRequest';

export function reasonLine(reason: ComposerReason): string | null {
  switch (reason) {
    case 'micOff':
    case 'micCheck':
      return 'Microphone is off · typing instead';
    case 'edit':
    case null:
      return null;
  }
}
