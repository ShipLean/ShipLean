import { describe, expect, it } from 'vitest';
import { bug, story } from '../ct/fixtures';
import type { LinkDto } from '../../../api/workItems';
import { changedFields, changesOf, draftOf, newDraftLink, rebase, type Draft } from './draft';

describe('draft', () => {
  it('changedFields_WhenUntouched_ShouldBeEmpty', () => {
    expect(changedFields(draftOf(story), draftOf(story))).toEqual([]);
  });

  it('changedFields_WhenTitleOnlyGainsWhitespace_ShouldBeEmpty', () => {
    const draft = { ...draftOf(story), title: `${story.title}  ` };
    expect(changedFields(draftOf(story), draft)).toEqual([]);
  });

  it('changedFields_WhenSeveralEdited_ShouldListThemInFormOrder', () => {
    const draft = { ...draftOf(story), tags: [], title: 'New', points: null };
    expect(changedFields(draftOf(story), draft)).toEqual(['title', 'points', 'tags']);
  });

  it('changedFields_WhenAssigneeCleared_ShouldCountIt', () => {
    expect(changedFields(draftOf(story), { ...draftOf(story), assignee: null })).toEqual([
      'assignee',
    ]);
  });

  it('changesOf_WhenOneFieldEdited_ShouldSetOnlyThatField', () => {
    const changes = changesOf(draftOf(bug), { ...draftOf(bug), severity: null });
    expect(changes.severity).toEqual({ value: null });
    expect(changes.title).toBeNull();
    expect(changes.reproSteps).toBeNull();
    expect(changes.addLinks).toEqual([]);
    expect(changes.removeLinkIds).toEqual([]);
  });

  it('changesOf_WhenParentAndSprintCleared_ShouldSendNullIds', () => {
    const base = {
      ...draftOf(story),
      parent: {
        id: 'f1',
        key: 'SHIP-101',
        type: { id: 'feature' as const, name: 'Feature' },
        title: 'F',
      },
    };
    const changes = changesOf(base, { ...base, parent: null, sprint: null });
    expect(changes.parent).toEqual({ value: null });
    expect(changes.sprint).toEqual({ value: null });
  });

  const related: LinkDto = {
    id: 'l1',
    type: 'related',
    workItemId: 'w144',
    key: 'SHIP-144',
    title: 'Saved cards list',
    url: null,
    card: null,
    autoLinked: null,
  };
  const withLink = { ...story, links: [related] };
  const added = () =>
    newDraftLink({
      type: 'blockedBy',
      workItemId: 'w120',
      key: 'SHIP-120',
      title: null,
      url: null,
      card: null,
      autoLinked: null,
    });

  it('changesOf_WhenLinkAddedAndOneRemoved_ShouldSendAddAndRemoveIds', () => {
    const base = draftOf(withLink);
    const draft: Draft = { ...base, links: [added()] };
    const changes = changesOf(base, draft);
    expect(changedFields(base, draft)).toEqual(['links']);
    expect(changes.addLinks).toEqual([
      { type: 'blockedBy', workItemId: 'w120', key: 'SHIP-120', title: null, url: null },
    ]);
    expect(changes.removeLinkIds).toEqual(['l1']);
  });

  it('changesOf_WhenLinksUntouched_ShouldSendNoLinkChanges', () => {
    const base = draftOf(withLink);
    expect(changesOf(base, base).addLinks).toEqual([]);
    expect(changesOf(base, base).removeLinkIds).toEqual([]);
  });

  it('rebase_WhenTheirsChangedAnotherField_ShouldKeepMineAndTakeTheirs', () => {
    const from = draftOf(story);
    const mine = { ...from, points: 13 };
    const to = draftOf({ ...story, revision: 8, priority: 1, points: 3 });
    const result = rebase(from, mine, to);
    expect(result.points).toBe(13);
    expect(result.priority).toBe(1);
  });

  it('rebase_WhenMineAddedALinkAndTheirsRemovedAnother_ShouldKeepTheirRemovalAndMyAdd', () => {
    const from = draftOf(withLink);
    const mine = { ...from, links: [...from.links, added()] };
    const to = draftOf(story);
    const result = rebase(from, mine, to);
    expect(result.links.map((link) => link.key)).toEqual(['SHIP-120']);
  });

  it('rebase_WhenMineRemovedALinkTheirsAlsoRemoved_ShouldNotSendItsRemoval', () => {
    const from = draftOf(withLink);
    const mine = { ...from, links: [] };
    const result = rebase(from, mine, draftOf(story));
    expect(changesOf(draftOf(story), result).removeLinkIds).toEqual([]);
  });

  it('rebase_WhenSentLinkIsNowSavedAndMineRemovedIt_ShouldDropItBySignature', () => {
    const sent: Draft = { ...draftOf(story), links: [added()] };
    const mine = { ...sent, links: [] };
    const saved = draftOf({
      ...story,
      links: [{ ...related, id: 'l9', workItemId: 'w120', key: 'SHIP-120', type: 'blockedBy' }],
    });
    expect(rebase(sent, mine, saved).links).toEqual([]);
  });

  it('rebase_WhenMineRemovedASavedLinkAndToStillHasIt_ShouldStayRemoved', () => {
    const from = draftOf(withLink);
    const mine = { ...from, links: [] };
    const to = draftOf({ ...withLink, revision: 8 });
    expect(rebase(from, mine, to).links).toEqual([]);
  });

  it('rebase_WhenToAlreadyHoldsMyNewLink_ShouldNotDuplicateIt', () => {
    const from = draftOf(story);
    const mine: Draft = { ...from, links: [added()] };
    const to = draftOf({
      ...story,
      revision: 8,
      links: [{ ...related, id: 'l9', workItemId: 'w120', key: 'SHIP-120', type: 'blockedBy' }],
    });
    expect(rebase(from, mine, to).links.map((link) => link.key)).toEqual(['SHIP-120']);
  });

  const feature = {
    ...story,
    type: { id: 'feature' as const, name: 'Feature' },
    goal: 'Pay in two taps',
    statusLine: 'Saved cards are on staging',
  };

  it('draftOf_WhenFeature_ShouldHoldGoalAndStatusLine', () => {
    expect(draftOf(feature)).toMatchObject({
      goal: 'Pay in two taps',
      statusLine: 'Saved cards are on staging',
    });
  });

  it('changedFields_WhenGoalAndStatusLineEdited_ShouldListThemAfterTheStripBeforeDescription', () => {
    const draft = { ...draftOf(feature), description: 'New', goal: 'G', statusLine: 'S', tags: [] };
    expect(changedFields(draftOf(feature), draft)).toEqual([
      'tags',
      'statusLine',
      'goal',
      'description',
    ]);
  });

  it('changesOf_WhenGoalAndStatusLineEdited_ShouldSetBoth', () => {
    const draft = { ...draftOf(feature), goal: 'Two taps', statusLine: 'Load testing next' };
    const changes = changesOf(draftOf(feature), draft);
    expect(changes.goal).toEqual({ value: 'Two taps' });
    expect(changes.statusLine).toEqual({ value: 'Load testing next' });
    expect(changes.title).toBeNull();
  });

  it('changesOf_WhenGoalAndStatusLineUntouched_ShouldSendNull', () => {
    const changes = changesOf(draftOf(feature), { ...draftOf(feature), title: 'Checkout v3' });
    expect(changes.goal).toBeNull();
    expect(changes.statusLine).toBeNull();
  });

  it('rebase_WhenMineEditedGoal_ShouldKeepMyGoalAndTakeTheirStatusLine', () => {
    const from = draftOf(feature);
    const mine = { ...from, goal: 'Mine' };
    const to = { ...from, statusLine: 'Theirs' };
    expect(rebase(from, mine, to)).toMatchObject({ goal: 'Mine', statusLine: 'Theirs' });
  });
});
