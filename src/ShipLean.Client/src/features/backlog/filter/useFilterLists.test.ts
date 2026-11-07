import { describe, expect, it } from 'vitest';
import type { StateDto } from '../../../api/projects';
import type { WorkItemTypeId } from '../../../api/refs';
import { levelStates } from './useFilterLists';

const state = (type: WorkItemTypeId, name: string): StateDto => ({
  id: `${type}-${name.toLowerCase()}`,
  type,
  name,
  category: name === 'New' ? 'proposed' : 'inProgress',
  order: 1,
  itemCount: 0,
});

// As the project's states read lists them: Epic's first, then Feature's, then the rest.
const STATES: StateDto[] = [
  state('epic', 'New'),
  state('epic', 'Active'),
  state('feature', 'New'),
  state('feature', 'Active'),
  state('story', 'New'),
  state('bug', 'New'),
  state('bug', 'Triaged'),
  state('task', 'New'),
  state('action', 'New'),
  state('bau', 'New'),
];

describe('levelStates', () => {
  it('levelStates_WhenFeatures_ShouldOfferOnlyFeatureStates', () => {
    expect(levelStates(STATES, 'features')).toEqual([
      { id: 'feature-new', name: 'New', category: 'proposed' },
      { id: 'feature-active', name: 'Active', category: 'inProgress' },
    ]);
  });

  it('levelStates_WhenEpics_ShouldOfferOnlyEpicStates', () => {
    expect(levelStates(STATES, 'epics').map((s) => s.id)).toEqual(['epic-new', 'epic-active']);
  });

  it('levelStates_WhenStoriesShareAName_ShouldSayWhoseEachIs', () => {
    expect(levelStates(STATES, 'stories').map((s) => [s.id, s.name])).toEqual([
      ['story-new', 'New (User Story)'],
      ['bug-new', 'New (Bug)'],
      ['bug-triaged', 'Triaged'],
      ['action-new', 'New (Action item)'],
    ]);
  });
});
