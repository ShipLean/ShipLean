import type { StateCategory } from '@shiplean/core/api/refs';
import type { RollupCountDto } from '@shiplean/core/api/views';

type Noun = [singular: string, plural: string];

// What a Feature's roll-ups count, in the order the web lists them (the SPA's doneConfirm/openText.ts, Features level).
const NOUNS: [RollupCountDto['type'], Noun][] = [
  ['story', ['story', 'stories']],
  ['bug', ['bug', 'bugs']],
  ['action', ['action item', 'action items']],
  ['feature', ['nested Feature', 'nested Features']],
];

const openOf = (rollups: RollupCountDto[], type: RollupCountDto['type']): number =>
  rollups.find((r) => r.type === type)?.open ?? 0;

// How many stories, bugs, action items and nested Features are still open under the Feature.
export function openCount(rollups: RollupCountDto[]): number {
  return NOUNS.reduce((sum, [type]) => sum + openOf(rollups, type), 0);
}

// "2 stories, 1 bug and 1 nested Feature". Empty when nothing is open.
export function openText(rollups: RollupCountDto[]): string {
  const parts = NOUNS.flatMap(([type, noun]) => {
    const open = openOf(rollups, type);
    return open > 0 ? [`${open} ${open === 1 ? noun[0] : noun[1]}`] : [];
  });
  return parts.length < 2
    ? (parts[0] ?? '')
    : `${parts.slice(0, -1).join(', ')} and ${parts[parts.length - 1]}`;
}

// D8: closing a Feature that still has open children is allowed, with this line on it. Null for any other target state,
// or when nothing is open.
export function closingWarning(rollups: RollupCountDto[], target: StateCategory): string | null {
  if (target !== 'completed') return null;
  const text = openText(rollups);
  return text === '' ? null : `${text} still open`;
}
