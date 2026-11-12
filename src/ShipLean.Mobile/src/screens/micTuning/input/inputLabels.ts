import type { AudioInput } from '../../../voice/contracts';

// The phone is named for how voice uses it; the others by the name the OS gives them.
export const inputTitle = (i: AudioInput): string =>
  i.kind === 'phone' ? 'Phone · speakerphone' : i.name;

export const inputDetail = (i: AudioInput): string => {
  if (i.kind === 'phone') return 'Its own microphone and loudspeaker · default';
  if (!i.connected) return 'Not connected';
  return `${i.kind === 'bluetooth' ? 'Bluetooth' : 'Wired'} · connected`;
};

// When the input was last tested: today, yesterday, N days ago, or the date. null when it never was.
export function testedLabel(testedAt: string, now: Date): string | null {
  const at = new Date(testedAt);
  if (testedAt === '' || Number.isNaN(at.getTime())) return null;
  const dayOf = (d: Date) =>
    Math.floor((d.getTime() - d.getTimezoneOffset() * 60_000) / 86_400_000);
  const days = dayOf(now) - dayOf(at);
  if (days <= 0) return 'today';
  if (days === 1) return 'yesterday';
  if (days < 14) return `${days} days ago`;
  return at.toISOString().slice(0, 10);
}
