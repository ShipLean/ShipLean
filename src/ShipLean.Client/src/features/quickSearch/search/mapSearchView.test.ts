import { describe, expect, it } from 'vitest';
import fixture from '../../../../../ShipLean.TestSupport/DtoFixtures/search/search-view.json';
import type { SearchViewDto } from '../../../api/search';
import { mapSearchView } from './mapSearchView';

const view = fixture as SearchViewDto;
const navigate = (): void => undefined;
const context = { currentKey: 'PAY', memberCounts: new Map([['SHIP', 4]]), navigate };

function items(dto: SearchViewDto, currentKey: string | undefined) {
  const group = mapSearchView(dto, { ...context, currentKey }).groups.find((g) => g.id === 'items');
  return group?.rows ?? [];
}

describe('mapSearchView', () => {
  it('mapSearchView_WhenItemsFromTwoProjects_ShouldPutTheCurrentProjectFirstKeepingOrder', () => {
    expect(items(view, 'PAY').map((r) => r.id)).toEqual(['PAY-5', 'SHIP-12', 'SHIP-31', 'SHIP-48']);
    // Fails on a sort: SHIP's three would come out in another order, or the server's order would be lost.
    expect(items(view, 'SHIP').map((r) => r.id)).toEqual([
      'SHIP-12',
      'SHIP-31',
      'SHIP-48',
      'PAY-5',
    ]);
    expect(items(view, undefined).map((r) => r.id)).toEqual([
      'SHIP-12',
      'SHIP-31',
      'SHIP-48',
      'PAY-5',
    ]);
  });

  it('mapSearchView_WhenMatchedInACommentOrAttachment_ShouldSayWhere', () => {
    const rows = items(view, undefined);
    expect(rows.find((r) => r.id === 'SHIP-31')).toMatchObject({
      match: { in: 'comment', snippet: 'The export fails on the late crossing.' },
    });
    expect(rows.find((r) => r.id === 'SHIP-48')).toMatchObject({
      match: { in: 'attachment', name: 'q3-export.csv', snippet: 'export of all rows' },
    });
    expect(rows.find((r) => r.id === 'PAY-5')).toMatchObject({
      match: { in: 'description', snippet: 'Rounds on export' },
    });
  });

  it('mapSearchView_WhenMatchedInTitle_ShouldHaveNoWhereLine', () => {
    expect(items(view, undefined).find((r) => r.id === 'SHIP-12')).toMatchObject({ match: null });
    const tagged = { ...view, items: [{ ...view.items[0], matchedIn: 'tags' as const }] };
    expect(items(tagged, undefined)[0]).toMatchObject({ match: null });
  });

  it('mapSearchView_WhenAGroupIsEmpty_ShouldLeaveItOut', () => {
    const mapped = mapSearchView({ ...view, sprints: [], items: [] }, context);
    expect(mapped.groups.map((g) => g.id)).toEqual(['projects']);
  });

  it('mapSearchView_WhenProjectsListed_ShouldDetailCurrentAndMembers', () => {
    const projects = mapSearchView(view, { ...context, currentKey: 'PAY' }).groups.find(
      (g) => g.id === 'projects',
    );
    expect(projects?.rows.map((r) => (r.kind === 'project' ? r.detail : ''))).toEqual([
      '4 members',
      'current',
    ]);
  });

  it('mapSearchView_WhenMoreResults_ShouldSayTop8ElseNameTheCurrentProject', () => {
    expect(mapSearchView(view, context).note).toBe('Top 8 items · keep typing to narrow');
    expect(mapSearchView({ ...view, more: false }, context).note).toBe(
      'All your projects · PAY first',
    );
    expect(
      mapSearchView({ ...view, more: false }, { ...context, currentKey: undefined }).note,
    ).toBe('All your projects');
  });
});
