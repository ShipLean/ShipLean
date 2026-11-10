// What the Backlog answers to a question about the list on screen. Read-only, shown as a sheet, never spoken.
import type { BacklogRowDto } from '@shiplean/core/api/views';
import type { AnswerSheetContent } from '../voice/contracts';
import { needsLine } from './list/filters';

export type AskTopic = 'whatsBlocked' | 'whatNeedsMe' | 'whatsNext';

const NEXT_COUNT = 3;

const plural = (n: number, one: string) => `${n} ${one}${n === 1 ? '' : 's'}`;

const rankOrder = (rows: BacklogRowDto[]) =>
  rows
    .filter((r) => r.kind === 'item' && r.rank !== null)
    .sort((a, b) => (a.rank ?? 0) - (b.rank ?? 0));

export function answerFor(topic: AskTopic, rows: BacklogRowDto[]): AnswerSheetContent {
  const ordered = rankOrder(rows);
  switch (topic) {
    case 'whatsBlocked': {
      const blocked = ordered.filter((r) => r.blockedBy.length > 0);
      return {
        lead:
          blocked.length === 0
            ? 'Nothing here is blocked.'
            : `${plural(blocked.length, 'item')} blocked`,
        rows: blocked.map((r) => ({
          key: r.key,
          title: r.title,
          counts: `blocked by ${r.blockedBy.join(', ')}`,
        })),
      };
    }
    case 'whatNeedsMe': {
      const waiting = ordered.filter((r) => r.needsYou !== null);
      return {
        lead:
          waiting.length === 0
            ? 'Nothing here waits on you.'
            : `${plural(waiting.length, 'item')} waiting on you`,
        rows: waiting.map((r) => ({
          key: r.key,
          title: r.title,
          counts: r.needsYou === null ? '' : needsLine(r.needsYou),
        })),
      };
    }
    case 'whatsNext': {
      const open = ordered.filter((r) => r.state?.category !== 'completed');
      const next = open.slice(0, NEXT_COUNT);
      return {
        lead:
          next.length === 0
            ? 'Nothing is open here.'
            : `Next ${plural(next.length, 'item')} by rank`,
        rows: next.map((r) => ({ key: r.key, title: r.title, counts: `rank ${r.rank}` })),
      };
    }
  }
}
