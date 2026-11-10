// The guided first command (setup D13, P12): when the ready coach mark shows, when the first answer opens the
// first-result sheet, and the Inbox tip. The flags are kept per phone, outside any account, like setupOffered.
import type { InboxDto } from '@shiplean/core/api/inbox';
import type { KeyValueStore } from '@shiplean/core/platform';
import type { MicStatus, VoiceUiState } from '../voice/internals';
import type { ModelStatus } from '../voice/contracts';

export const GUIDE_KEYS = {
  // The "Try it" coach mark has been shown.
  voiceReadyShown: 'shiplean.voice.readyShown',
  // The "Turn on the microphone" coach mark has been shown. Its own flag, so it shows once and the "Try it" one still
  // follows when the microphone is first allowed.
  micCoachShown: 'shiplean.voice.micCoachShown',
  // The guide is over: the first-result sheet was closed, or the coach mark was skipped.
  firstCommandShown: 'shiplean.voice.firstCommandShown',
  // The Inbox tip after Open Inbox has been shown.
  fromSetupTipShown: 'shiplean.voice.fromSetupTipShown',
} as const;

export type GuideFlag = keyof typeof GUIDE_KEYS;
export type GuideFlags = Record<GuideFlag, boolean>;

export function readGuide(kv: KeyValueStore): GuideFlags {
  const read = (flag: GuideFlag) => kv.getItem(GUIDE_KEYS[flag]) !== null;
  return {
    voiceReadyShown: read('voiceReadyShown'),
    micCoachShown: read('micCoachShown'),
    firstCommandShown: read('firstCommandShown'),
    fromSetupTipShown: read('fromSetupTipShown'),
  };
}

export function markGuide(kv: KeyValueStore, flag: GuideFlag): void {
  kv.setItem(GUIDE_KEYS[flag], '1');
}

export type CoachVariant = 'try' | 'micOff';

// Which coach mark to show now, or null. Once voice is ready: "Try it" when the microphone is allowed; otherwise the
// turn-it-on one, once. The guided command waits for the "Try it" one, so it waits for the microphone.
export function coachVariant(
  phase: ModelStatus['phase'],
  mic: MicStatus,
  flags: GuideFlags,
): CoachVariant | null {
  if (phase !== 'ready') return null;
  if (flags.voiceReadyShown || flags.firstCommandShown) return null;
  if (mic === 'granted') return 'try';
  return flags.micCoachShown ? null : 'micOff';
}

// Can the phone's own prompt still be shown? Then the coach mark offers Allow microphone, else Open Settings.
export const canAskMic = (mic: MicStatus): boolean =>
  mic === 'undetermined' || mic === 'deniedCanAsk';

// Whether the voice state is the first answer to guide: a read-only answer after the coach mark. Any other result
// (a preview, a failure) goes on as normal and leaves the guide pending.
export function firstCommandStep(flags: GuideFlags, state: VoiceUiState): 'show' | 'wait' {
  if (!flags.voiceReadyShown || flags.firstCommandShown) return 'wait';
  return state.kind === 'answer' ? 'show' : 'wait';
}

export interface FirstResultView {
  lead: string;
  rows: { key: string; title: string; counts: string }[];
}

const count = (n: number, one: string, many: string) => `${n} ${n === 1 ? one : many}`;

function joinWords(parts: string[]): string {
  if (parts.length <= 1) return parts.join('');
  return `${parts.slice(0, -1).join(', ')} and ${parts[parts.length - 1]}`;
}

// The Inbox as it is now, in its order, with the counts of each kind of ask.
export function firstResultView(inbox: InboxDto): FirstResultView {
  const total = { decision: 0, question: 0, mention: 0, status: 0 };
  const rows = inbox.items.map((item) => {
    const own = { decision: 0, question: 0, mention: 0, status: 0 };
    for (const ask of item.asks) {
      own[ask.kind] += 1;
      total[ask.kind] += 1;
    }
    const counts = [
      own.decision > 0 ? count(own.decision, 'decision', 'decisions') : null,
      own.question > 0 ? count(own.question, 'question', 'questions') : null,
      own.mention > 0 ? count(own.mention, 'mention', 'mentions') : null,
      own.status > 0 ? 'status line' : null,
    ].filter((c): c is string => c !== null);
    return { key: item.key, title: item.title, counts: counts.join(' · ') };
  });
  if (rows.length === 0) return { lead: 'Nothing is waiting on you.', rows };
  const kinds = [
    total.decision > 0 ? count(total.decision, 'decision', 'decisions') : null,
    total.question > 0 ? count(total.question, 'question', 'questions') : null,
    total.mention > 0 ? (total.mention === 1 ? 'a mention' : `${total.mention} mentions`) : null,
    total.status > 0
      ? total.status === 1
        ? 'a status line to check'
        : `${total.status} status lines to check`
      : null,
  ].filter((k): k is string => k !== null);
  return {
    lead: `${count(rows.length, 'item waits', 'items wait')} on you: ${joinWords(kinds)}.`,
    rows,
  };
}
