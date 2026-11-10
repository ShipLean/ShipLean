// Every message for the failures that need a correction. Shown, never spoken (speak in, read out).
import type { NoValidActionReason } from '../contracts';
import type { VoiceSource } from '../internals';

export interface CommonCopy {
  nothingChanged: string;
  // Back to the words: say it again (voice) or edit the text (typed).
  fix: string;
  whatCanISay: string;
}

export function commonCopy(source: VoiceSource): CommonCopy {
  return {
    nothingChanged: 'Nothing was changed',
    fix: source === 'voice' ? 'Fix' : 'Edit',
    whatCanISay: 'What can I say here?',
  };
}

export interface SheetCopy {
  title: string;
  lead: string;
  detail: string | null;
}

// No item matched what was said (voice D5): the two closest are offered when there are any.
export function noMatchCopy(spoken: string, closest: number, offline: boolean): SheetCopy {
  return {
    title: 'Nothing matched',
    lead: `No match for “${spoken}”`,
    detail:
      closest > 0
        ? offline
          ? 'Closest on this screen:'
          : 'Closest in this project:'
        : offline
          ? "You're offline, so there's nothing to suggest."
          : 'Nothing close in this project.',
  };
}

// A key that doesn't exist but sounds like one that does (voice D6): exactly one suggestion.
export function misheardKeyCopy(spoken: string, suggestion: string): SheetCopy & { yes: string } {
  return {
    title: 'Nothing matched',
    lead: `There's no ${spoken}.`,
    detail: `Did you mean ${suggestion}?`,
    yes: `Yes, ${suggestion}`,
  };
}

// Two or more matches (E16): numbered in rank order.
export function ambiguousCopy(source: VoiceSource): SheetCopy {
  return {
    title: 'Which one?',
    lead: 'More than one matches',
    detail: source === 'voice' ? 'Tap one, or hold and say its number.' : 'Tap the one you meant.',
  };
}

export function candidateLabel(position: number, key: string, title: string): string {
  return `${position} · ${key} ${title}`;
}

const REASONS: Record<Exclude<NoValidActionReason, 'noSuchState'>, string> = {
  notAllowed: "You can't make that change here",
  invalidArgs: "That command isn't complete",
  none: "That isn't something this screen can do",
};

// An action that can't be taken (voice D8): a state that doesn't exist offers the type's states.
export function noValidActionCopy(reason: NoValidActionReason, states: number): SheetCopy {
  if (reason === 'noSuchState') {
    return {
      title: 'No such state',
      lead: "That state doesn't exist here",
      detail: states > 0 ? 'Pick one of these:' : null,
    };
  }
  return { title: 'Not possible here', lead: REASONS[reason], detail: null };
}

// A command the phone can't take (voice D7): the reason, and where it's done.
export function webOnlyCopy(where: string): SheetCopy & { open: string } {
  return {
    title: 'Done on the web',
    lead: "That's done on the web",
    detail: where,
    open: 'Open on the web',
  };
}

export const WHAT_CAN_I_SAY = {
  title: 'What can I say here?',
  empty: 'This screen has no voice commands.',
  webOnlyHeading: 'Only on the web',
  done: 'Done',
} as const;
