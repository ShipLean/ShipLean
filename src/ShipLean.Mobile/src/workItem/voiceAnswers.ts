// What the Work item screen answers to a question about the item on screen. Read-only, shown as a sheet, never spoken.
import type { ItemAsksDto } from '@shiplean/core/api/asks';
import type { WorkItemDto } from '@shiplean/core/api/workItems';
import type { AnswerSheetContent } from '../voice/contracts';

export type AskTopic = 'whatsLeft' | 'whatsBlocked' | 'whoOwns' | 'whatNeedsMe';

const plural = (n: number, one: string) => `${n} ${one}${n === 1 ? '' : 's'}`;

const isOpen = (category: string) => category !== 'completed' && category !== 'removed';

export function answerFor(
  topic: AskTopic,
  item: WorkItemDto,
  asks: ItemAsksDto | null,
): AnswerSheetContent {
  switch (topic) {
    case 'whatsLeft': {
      const left = item.children.filter((c) => isOpen(c.state.category));
      return {
        lead:
          left.length === 0
            ? `Nothing is left under ${item.key}.`
            : `${left.length} of ${plural(item.children.length, 'item')} left under ${item.key}`,
        rows: left.map((c) => ({ key: c.key, title: c.title, counts: c.state.name })),
      };
    }
    case 'whatsBlocked': {
      const blockers = item.links.filter((l) => l.type === 'blockedBy' && l.key !== null);
      const lead =
        blockers.length > 0
          ? `${item.key} is blocked by ${plural(blockers.length, 'item')}`
          : item.health === 'blocked'
            ? `${item.key} is marked blocked.`
            : `Nothing blocks ${item.key}.`;
      return {
        lead,
        rows: blockers.map((l) => ({
          key: l.key ?? '',
          title: l.title ?? '',
          counts: 'blocked by',
        })),
      };
    }
    case 'whoOwns': {
      const owned = item.children.filter((c) => c.assignee !== null);
      return {
        lead:
          item.assignee === null
            ? `${item.key} has no owner.`
            : `${item.key} is owned by ${item.assignee.name}`,
        rows: owned.map((c) => ({
          key: c.key,
          title: c.title,
          counts: c.assignee === null ? '' : c.assignee.name,
        })),
      };
    }
    case 'whatNeedsMe': {
      const questions = (asks?.questions ?? []).filter((q) => q.state === 'open');
      const decisions = (asks?.decisions ?? []).filter((d) => d.state === 'open');
      const waiting = asks?.waitingChildren ?? [];
      const total = questions.length + decisions.length + waiting.length;
      return {
        lead: total === 0 ? `Nothing on ${item.key} waits on you.` : `${total} waiting on you`,
        rows: [
          ...questions.map((q) => ({ key: q.label, title: q.text, counts: 'question' })),
          ...decisions.map((d) => ({ key: d.label, title: d.question, counts: 'decision' })),
          ...waiting.map((w) => ({
            key: w.key,
            title: w.title,
            counts: `${plural(w.openQuestions, 'question')}, ${plural(w.openDecisions, 'decision')}`,
          })),
        ],
      };
    }
  }
}
