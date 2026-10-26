import type { RollupCountDto } from '../../../../api/views';
import type { Level } from '../../../../routes';

type Noun = [singular: string, plural: string];

// The nouns for what a card's roll-ups count, in the order the design lists them.
const NOUNS: Record<Level, Partial<Record<RollupCountDto['type'], Noun>>> = {
  stories: {},
  features: {
    story: ['story', 'stories'],
    bug: ['bug', 'bugs'],
    feature: ['nested Feature', 'nested Features'],
  },
  epics: {
    feature: ['Feature', 'Features'],
    story: ['story', 'stories'],
    bug: ['bug', 'bugs'],
  },
};

/** What is still open under a card, by type: "2 stories, 1 bug and 1 nested Feature". Empty when nothing is. */
export function openText(rollups: RollupCountDto[], level: Level): string {
  const nouns = NOUNS[level];
  const parts = Object.entries(nouns).flatMap(([type, noun]) => {
    const open = rollups.find((rollup) => rollup.type === type)?.open ?? 0;
    return open > 0 ? [`${open} ${open === 1 ? noun[0] : noun[1]}`] : [];
  });
  return parts.length < 2
    ? (parts[0] ?? '')
    : `${parts.slice(0, -1).join(', ')} and ${parts[parts.length - 1]}`;
}

/** The board the open children are closed or moved from. */
export const boardOfChildren = (level: Level) => (level === 'epics' ? 'Features' : 'Stories');
