import type { InboxAskDto } from '@shiplean/core/api/inbox';
import { describe, expect, it } from 'vitest';
import { aPending } from '../test-support/actions';
import { designInbox } from '../test-support/inboxDesign';
import {
  aDecisionAsk,
  aMentionAsk,
  aQuestionAsk,
  aStatusAsk,
  anInboxItem,
} from '../test-support/inbox';
import type { InboxAction, PendingAnswer } from './actions';
import { labelsOf } from './actions';
import {
  buttonsFor,
  compressLabels,
  fromLine,
  kindDetail,
  kindIcon,
  kindLabel,
  moreLine,
  pendingFor,
  pendingLine,
  splitAsks,
  waitsLine,
} from './rowModel';

const decisions = (...numbers: number[]): InboxAskDto[] =>
  numbers.map((number) => aDecisionAsk({ number }));

const target = { area: 'inbox', projectId: 'p', itemId: 'i', key: 'SHIP-1' } as const;
const answer = (action: InboxAction, state: PendingAnswer['state']): PendingAnswer => ({
  ...aPending({ action, state }),
  action,
  labels: labelsOf(action),
});

describe('rowModel', () => {
  it('labels each kind and a mix of kinds', () => {
    expect(kindLabel(decisions(10))).toBe('Decision');
    expect(kindDetail(decisions(10))).toBe('D10');
    expect(kindLabel(decisions(1, 2, 3, 4, 5, 6, 7, 8))).toBe('8 decisions');
    expect(kindDetail(decisions(1, 2, 3, 4, 5, 6, 7, 8))).toBe('D1–D8');
    expect(kindDetail(decisions(1, 2, 4))).toBe('D1–D2, D4');
    expect(kindLabel([aQuestionAsk()])).toBe('Needs input');
    expect(kindDetail([aQuestionAsk()])).toBeNull();
    expect(kindLabel([aMentionAsk()])).toBe('Mentioned you');
    expect(kindLabel([aStatusAsk()])).toBe('Status line to check');
    expect(kindDetail([aStatusAsk()])).toBe('you own this');
    expect(kindLabel([aQuestionAsk(), ...decisions(10, 11)])).toBe('Needs input + 2 decisions');
    expect(kindDetail([aQuestionAsk(), ...decisions(10, 11)])).toBe('D10–D11');
    expect(kindIcon([aStatusAsk()])).toBe('clock');
    expect(kindIcon([aQuestionAsk()])).toBe('message');
    expect(kindIcon([aMentionAsk()])).toBe('user');
    expect(kindIcon(decisions(1))).toBe('check');
    expect(compressLabels(['D3', 'D1', 'D2', 'Q1'])).toBe('D1–D3, Q1');
  });

  it('writes the more line as "2 decisions too · D10, D11"', () => {
    const ship906 = designInbox().items.find((i) => i.key === 'SHIP-906')!;
    const { first, rest } = splitAsks(ship906);
    expect(first.map((a) => a.label)).toEqual(['Q1']);
    expect(moreLine(rest)).toBe('2 decisions too · D10, D11');
    expect(moreLine([])).toBeNull();
    expect(moreLine([aMentionAsk()])).toBe('1 mention too · @1');
    expect(moreLine([aMentionAsk({}, { label: '@2' }), aStatusAsk()])).toBe(
      '1 mention, status line too · @2',
    );
    expect(moreLine([aStatusAsk()])).toBe('Status line too');
  });

  it('opens with every decision when the first ask is a decision, and with one ask otherwise', () => {
    const ship907 = designInbox().items.find((i) => i.key === 'SHIP-907')!;
    expect(splitAsks(ship907).first).toHaveLength(8);
    expect(splitAsks(ship907).rest).toEqual([]);
    const mixed = anInboxItem({ asks: [...decisions(1, 2), aStatusAsk()] });
    expect(splitAsks(mixed).first.map((a) => a.label)).toEqual(['D1', 'D2']);
    expect(splitAsks(mixed).rest.map((a) => a.label)).toEqual(['Status']);
  });

  it('gives each kind of ask its buttons', () => {
    const ids = (asks: InboxAskDto[]) => buttonsFor(asks).map((b) => [b.id, b.label]);
    expect(ids(decisions(10))).toEqual([
      ['approve', 'Approve'],
      ['discuss', 'Discuss'],
    ]);
    expect(buttonsFor(decisions(10))[0]!.intent).toEqual({ kind: 'approve', number: 10 });
    expect(ids(decisions(1, 2, 3))).toEqual([
      ['review', 'Review all 3'],
      ['approve-all', 'Approve all 3'],
    ]);
    // Approve all is never one tap: both open the review sheet, one scrolled to Confirm.
    expect(buttonsFor(decisions(1, 2, 3)).map((b) => b.intent)).toEqual([
      { kind: 'review', scrollToConfirm: false },
      { kind: 'review', scrollToConfirm: true },
    ]);
    expect(ids([aQuestionAsk({ number: 7, options: ['20 MB', '50 MB'] })])).toEqual([
      ['option-0', '20 MB'],
      ['option-1', '50 MB'],
      ['reply', 'Reply'],
    ]);
    expect(
      buttonsFor([aQuestionAsk({ number: 7, options: ['20 MB', '50 MB'] })])[1]!.intent,
    ).toEqual({
      kind: 'option',
      number: 7,
      optionIndex: 1,
      option: '50 MB',
    });
    expect(ids([aQuestionAsk()])).toEqual([['reply', 'Reply']]);
    expect(ids([aMentionAsk()])).toEqual([
      ['reply', 'Reply'],
      ['seen', 'Seen'],
    ]);
    expect(ids([aStatusAsk()])).toEqual([
      ['still-true', 'Still true'],
      ['new-status', 'New status'],
    ]);
    expect(buttonsFor([])).toEqual([]);
  });

  it('gives each pending state its line', () => {
    const approve: InboxAction = { ...target, kind: 'approve', numbers: [10] };
    expect(pendingLine(answer(approve, 'undo'))).toBe('D10 approved · just now');
    expect(pendingLine(answer(approve, 'sent'))).toBe('D10 approved · just now');
    expect(pendingLine(answer(approve, 'queued'))).toBe("Sends when you're online");
    expect(pendingLine(answer(approve, 'sending'))).toBe('Sending…');
    expect(pendingLine(answer(approve, 'uncertain'))).toBe('Sending…');
    expect(pendingLine(answer(approve, 'failed'))).toBe("Didn't send · see Answered");
    expect(pendingLine(answer({ ...target, kind: 'approve', numbers: [1, 2, 3, 4] }, 'undo'))).toBe(
      'D1–D4 approved · just now',
    );
    expect(pendingLine(answer({ ...target, kind: 'stillTrue', revision: 3 }, 'undo'))).toBe(
      'Status still true · just now',
    );
  });

  it('finds the latest answer in play for an ask, ignoring undone ones', () => {
    const first = answer({ ...target, kind: 'approve', numbers: [10] }, 'failed');
    const second = answer({ ...target, kind: 'approve', numbers: [10] }, 'undo');
    const undone = {
      ...answer({ ...target, kind: 'approve', numbers: [11] }, 'undo'),
      undoOutcome: 'undone' as const,
    };
    expect(pendingFor(['D10'], [first, second])).toBe(second);
    expect(pendingFor(['D11'], [undone])).toBeNull();
    expect(pendingFor(['D12'], [first, second])).toBeNull();
    expect(pendingFor(['D1', 'D10'], [first])).toBe(first);
  });

  it('says what waits on an item and who asked', () => {
    const ship907 = designInbox().items.find((i) => i.key === 'SHIP-907')!;
    expect(waitsLine(ship907)).toBe('SHIP-906 waits on this');
    expect(
      waitsLine(
        anInboxItem({
          waitedOnBy: [
            { key: 'SHIP-1', title: 'a' },
            { key: 'SHIP-2', title: 'b' },
          ],
          waitedOnByMore: 2,
        }),
      ),
    ).toBe('SHIP-1, SHIP-2 and 2 more wait on this');
    expect(waitsLine(anInboxItem())).toBeNull();
    expect(fromLine(aDecisionAsk())).toBe('Claude Code');
    expect(fromLine(aDecisionAsk({}, { agentName: 'Claude Code (claude.ai)' }))).toBe(
      'Claude Code',
    );
    expect(fromLine(aMentionAsk())).toBe('Priya Shah');
    expect(fromLine(aStatusAsk({}, { atLabel: '4 days ago' }))).toBe(
      'Checked by Priya Shah · 4 days ago',
    );
    expect(fromLine(aStatusAsk({}, { from: null }))).toBe('Never checked');
  });
});
