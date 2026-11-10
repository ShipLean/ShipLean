// Pure answers to the questions the Retro tab can be asked, from the points on screen.
import type { AnswerSheetContent } from '../voice/contracts';
import type { RetroPointDto } from '@shiplean/core/api/retros';
import { CATEGORY_LABELS } from './notes/notesModel';

export type RetroAskTopic = 'keepsGoingWrong' | 'leftToDecide' | 'whatWentWell';

const plural = (n: number, word: string) => `${n} ${word}${n === 1 ? '' : 's'}`;
const keyOf = (p: RetroPointDto) => p.workItem?.key ?? '';

export function retroAnswer(topic: RetroAskTopic, points: RetroPointDto[]): AnswerSheetContent {
  switch (topic) {
    case 'keepsGoingWrong': {
      const recurring = points.filter((p) => p.recurring);
      const themes = new Map<string, number>();
      for (const p of recurring)
        themes.set(p.theme?.name ?? 'No theme', (themes.get(p.theme?.name ?? 'No theme') ?? 0) + 1);
      return {
        lead:
          themes.size === 0
            ? 'Nothing is marked as recurring.'
            : `${plural(themes.size, 'theme')} keep coming back.`,
        rows: [...themes].map(([name, n]) => ({
          key: '',
          title: name,
          counts: plural(n, 'point'),
        })),
      };
    }
    case 'leftToDecide': {
      const open = points.filter((p) => p.decision === 'undecided');
      return {
        lead:
          open.length === 0
            ? 'Every point is decided.'
            : `${plural(open.length, 'point')} still need a decision.`,
        rows: open.map((p) => ({
          key: keyOf(p),
          title: p.text,
          counts: `${CATEGORY_LABELS[p.category]} · +${p.supporters.length}`,
        })),
      };
    }
    case 'whatWentWell': {
      const well = points.filter((p) => p.category === 'wentWell');
      return {
        lead:
          well.length === 0
            ? 'No Went well points yet.'
            : `${plural(well.length, 'point')} went well.`,
        rows: well.map((p) => ({
          key: keyOf(p),
          title: p.text,
          counts: `+${p.supporters.length}`,
        })),
      };
    }
  }
}
