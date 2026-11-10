import type { AskHistoryLineDto } from '@shiplean/core/api/asks';
import type { CommentDto } from '@shiplean/core/api/comments';
import type { FieldChange } from '@shiplean/core/api/persisted';
import type { RevisionDto } from '@shiplean/core/api/workItems';
import { describe, expect, it } from 'vitest';
import { aComment, aHistory } from '../../test-support/workItem';
import { askLine, changeLine, filterTimeline, mergeTimeline } from './timeline';

const note = (id: string, createdAt: string, over: Partial<CommentDto> = {}) =>
  aComment('item', { id, createdAt, ...over });

const revision = (rev: number, changedAt: string): RevisionDto => ({
  ...aHistory().revisions[0],
  rev,
  changedAt,
});

const ask = (
  badge: string,
  at: string,
  over: Partial<AskHistoryLineDto> = {},
): AskHistoryLineDto => ({
  kind: 'confirmed',
  badge,
  from: null,
  to: null,
  note: null,
  by: null,
  agentName: null,
  at,
  atLabel: at,
  ...over,
});

const ids = (entries: { id: string }[]) => entries.map((entry) => entry.id);

describe('timeline', () => {
  // Red when the list is grouped by kind, sorted oldest first, or sorted by the server's label text.
  it('merges comments, revisions and ask events newest first', () => {
    const entries = mergeTimeline(
      [note('c-old', '2030-01-01T08:00:00+00:00'), note('c-new', '2030-01-01T12:00:00+00:00')],
      {
        revisions: [
          revision(7, '2030-01-01T11:00:00+00:00'),
          revision(6, '2030-01-01T09:00:00+00:00'),
        ],
        asks: [ask('D10', '2030-01-01T10:00:00+00:00'), ask('D9', '2030-01-01T07:00:00+00:00')],
      },
    );
    expect(ids(entries)).toEqual([
      'c-new',
      'rev-7',
      'ask-confirmed-D10-2030-01-01T10:00:00+00:00',
      'rev-6',
      'c-old',
      'ask-confirmed-D9-2030-01-01T07:00:00+00:00',
    ]);
  });

  it('orders the same instant in different offsets by time, and a tie comments first', () => {
    const entries = mergeTimeline([note('c', '2030-01-01T10:00:00+00:00')], {
      revisions: [revision(3, '2030-01-01T17:00:00+07:00')],
      asks: [ask('D1', '2030-01-01T10:00:00Z')],
    });
    expect(ids(entries)).toEqual(['c', 'rev-3', 'ask-confirmed-D1-2030-01-01T10:00:00Z']);
  });

  it('merges what is there when one half is missing', () => {
    expect(ids(mergeTimeline(undefined, aHistory({ asks: [] })))).toEqual(
      aHistory().revisions.map((r) => `rev-${r.rev}`),
    );
    expect(ids(mergeTimeline([note('c', '2030-01-01T10:00:00+00:00')], undefined))).toEqual(['c']);
  });

  // The server never returns an archived comment, so the list holds exactly what it sent: nothing is dropped, and a
  // superseded comment (kept, struck through) stays. Red when superseded comments are filtered out.
  it('leaves archived comments out', () => {
    const superseded = note('c-s', '2030-01-01T09:00:00+00:00', {
      superseded: {
        at: '2030-01-01T09:30:00+00:00',
        atLabel: '1 Jan',
        by: { id: 'u', name: 'Priya Shah', initials: 'PS', avatarColour: '#000', photoUrl: null },
      },
    });
    const kept = note('c-k', '2030-01-01T10:00:00+00:00');
    expect(ids(mergeTimeline([kept, superseded], { revisions: [], asks: [] }))).toEqual([
      'c-k',
      'c-s',
    ]);
    expect(ids(mergeTimeline([kept], { revisions: [], asks: [] }))).toEqual(['c-k']);
  });

  // Red when Changes keeps notes, Notes keeps revisions or ask events, or All drops a kind.
  it('filters by kind', () => {
    const entries = mergeTimeline([note('c', '2030-01-01T12:00:00+00:00')], {
      revisions: [revision(2, '2030-01-01T11:00:00+00:00')],
      asks: [ask('D1', '2030-01-01T10:00:00+00:00')],
    });
    expect(ids(filterTimeline(entries, 'all'))).toHaveLength(3);
    expect(ids(filterTimeline(entries, 'notes'))).toEqual(['c']);
    expect(ids(filterTimeline(entries, 'changes'))).toEqual([
      'rev-2',
      'ask-confirmed-D1-2030-01-01T10:00:00+00:00',
    ]);
  });

  it('words a change from the server labels, and says changed when there is nothing short to show', () => {
    const change = (over: Partial<FieldChange>): FieldChange => ({
      field: 'state',
      from: 'a',
      to: 'b',
      fromLabel: null,
      toLabel: null,
      ...over,
    });
    expect(changeLine(change({ fromLabel: 'New', toLabel: 'Active' }))).toBe('State New → Active');
    expect(changeLine(change({ field: 'assignee', toLabel: 'Priya Shah' }))).toBe(
      'Owner — → Priya Shah',
    );
    expect(changeLine(change({ field: 'priority', from: 2, to: 1 }))).toBe('Priority 2 → 1');
    expect(changeLine(change({ field: 'description', from: 'x'.repeat(80), to: 'y' }))).toBe(
      'Description changed',
    );
  });

  it('words an ask event from its badge', () => {
    expect(askLine(ask('D10', 'x'))).toBe('D10 approved');
    expect(askLine(ask('Q1', 'x', { kind: 'answered', to: 'Yes' }))).toBe('Q1 answered Yes');
    expect(askLine(ask('D3', 'x', { kind: 'changed', from: 'Yes', to: 'No' }))).toBe(
      'D3 changed Yes → No',
    );
  });
});
