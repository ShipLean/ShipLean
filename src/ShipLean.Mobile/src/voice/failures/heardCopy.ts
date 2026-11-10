// Every message for the failures where nothing usable was heard. Shown, never spoken (speak in, read out).
import type { VoiceSource } from '../internals';

export interface ToastCopy {
  text: string;
  ms: number;
}

export const TOO_SHORT_MS = 3000;
export const NOTHING_HEARD_MS = 4000;

// A tap under half a second (voice D2).
export function tooShortToast(): ToastCopy {
  return { text: 'Hold the mic while you speak', ms: TOO_SHORT_MS };
}

// Held but silent (voice D1): says nothing was changed, and which microphone was listening when the phone knows.
export function nothingHeardToast(inputName: string | null): ToastCopy {
  const base = "Didn't hear anything · nothing was changed";
  return { text: inputName === null ? base : `${base} · ${inputName}`, ms: NOTHING_HEARD_MS };
}

export interface MicCheckCopy {
  title: string;
  lead: string;
  detail: string;
  levelQuiet: string;
  levelHearing: string;
  unavailable: string;
  noInput: string;
  inputDetail: string;
  typeInstead: string;
  done: string;
}

// The third silent hold in a row (voice D3).
export const MIC_CHECK_COPY: MicCheckCopy = {
  title: 'Mic check',
  lead: 'Still not hearing you',
  detail: 'Three tries in a row came through silent. Say something now to test.',
  levelQuiet: 'Quiet',
  levelHearing: 'Hearing you',
  unavailable: "The microphone couldn't start, so there's no level to show.",
  noInput: "The phone didn't say which microphone it's using.",
  inputDetail: 'The phone listens through it',
  typeInstead: 'Type instead',
  done: 'Done',
};

export interface UnclearCopy {
  title: string;
  lead: string;
  detail: string;
  // Keeps the words as a note: voice previews it first, typed posts it at once (typed D6).
  note: string;
  // Back to the words: say it again (voice) or edit the text (typed).
  fix: string;
  whatCanISay: string;
  nothingChanged: string;
}

// Words with no change in them (voice D9, typed D6).
export function unclearCopy(source: VoiceSource): UnclearCopy {
  const common = {
    title: 'No change found',
    lead: "That isn't a change I can make",
    whatCanISay: 'What can I say here?',
    nothingChanged: 'Nothing was changed',
  };
  return source === 'voice'
    ? {
        ...common,
        detail: 'Keep it as a note, or say it again.',
        note: 'Add as a note',
        fix: 'Fix',
      }
    : {
        ...common,
        detail: 'Post it as a note instead, word for word?',
        note: 'Post as a note',
        fix: 'Edit',
      };
}
