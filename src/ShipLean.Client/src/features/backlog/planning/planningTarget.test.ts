import { describe, expect, it } from 'vitest';
import { planningView } from '../ct/fixtures';
import {
  dropByKey,
  dropOnRow,
  dropOnSection,
  sectionDropId,
  sectionOfDropId,
} from './planningTarget';

const { sections } = planningView();
// Sprint 24: SHIP-160. Sprint 25: SHIP-158. Sprint 26: empty. Backlog: SHIP-170, SHIP-171, SHIP-172.
const id = (key: string) => `id-${key}`;

describe('dropOnRow', () => {
  it('Drop_WhenAboveARowOfAnotherSection_ShouldSetItsSprintAndGoJustAboveIt', () => {
    expect(dropOnRow(sections, id('SHIP-160'), id('SHIP-171'), false)).toMatchObject({
      sectionId: 'backlog',
      sprint: null,
      sprintChanged: true,
      beforeItemId: id('SHIP-171'),
      done: 'SHIP-160 → Backlog',
    });
  });

  it('Drop_WhenBelowTheLastRowOfAnotherSection_ShouldGoLast', () => {
    expect(dropOnRow(sections, id('SHIP-160'), id('SHIP-172'), true)).toMatchObject({
      sectionId: 'backlog',
      beforeItemId: null,
      placeBefore: null,
    });
  });

  it('Drop_WhenIntoASprint_ShouldNameTheSprint', () => {
    expect(dropOnRow(sections, id('SHIP-170'), id('SHIP-158'), false)).toMatchObject({
      sprint: { id: 'sp25' },
      sprintChanged: true,
      done: 'SHIP-170 → Sprint 25',
    });
  });

  it('Drop_WhenInTheSameSection_ShouldReorderWithoutChangingTheSprint', () => {
    expect(dropOnRow(sections, id('SHIP-170'), id('SHIP-172'), true)).toMatchObject({
      sprintChanged: false,
      beforeItemId: null,
      done: 'SHIP-170 reordered',
    });
  });

  it('Drop_WhenItLandsWhereItAlreadyIs_ShouldMoveNothing', () => {
    // Below its upper neighbour is the place it already holds; so is dropping it on itself.
    expect(dropOnRow(sections, id('SHIP-171'), id('SHIP-170'), true)).toBeNull();
    expect(dropOnRow(sections, id('SHIP-171'), id('SHIP-171'), false)).toBeNull();
  });
});

describe('dropOnSection', () => {
  it('Drop_WhenOnAnotherSectionsHeader_ShouldChangeTheSprintAndKeepTheOrder', () => {
    expect(dropOnSection(sections, id('SHIP-170'), 'sp26')).toMatchObject({
      sprint: { id: 'sp26' },
      sprintChanged: true,
      beforeItemId: undefined,
      placeBefore: null,
    });
  });

  it('Drop_WhenOnItsOwnSectionsHeader_ShouldMoveNothing', () => {
    expect(dropOnSection(sections, id('SHIP-170'), 'backlog')).toBeNull();
  });
});

describe('dropByKey', () => {
  it('Key_WhenInsideASection_ShouldMoveOnePlace', () => {
    expect(dropByKey(sections, id('SHIP-171'), 'up')).toMatchObject({
      sprintChanged: false,
      beforeItemId: id('SHIP-170'),
    });
    expect(dropByKey(sections, id('SHIP-171'), 'down')).toMatchObject({
      sprintChanged: false,
      beforeItemId: null,
    });
  });

  it('Key_WhenAtTheTopOfASection_ShouldGoLastInTheSectionAbove', () => {
    expect(dropByKey(sections, id('SHIP-170'), 'up')).toMatchObject({
      sectionId: 'sp26',
      sprintChanged: true,
      // Sprint 26 is empty: the sprint changes and there is no place to name.
      beforeItemId: undefined,
    });
    expect(dropByKey(sections, id('SHIP-158'), 'up')).toMatchObject({
      sectionId: 'sp24',
      beforeItemId: null,
    });
  });

  it('Key_WhenAtTheBottomOfASection_ShouldGoFirstInTheSectionBelow', () => {
    expect(dropByKey(sections, id('SHIP-160'), 'down')).toMatchObject({
      sectionId: 'sp25',
      beforeItemId: id('SHIP-158'),
      placeBefore: id('SHIP-158'),
    });
    expect(dropByKey(sections, id('SHIP-158'), 'down')).toMatchObject({
      sectionId: 'sp26',
      beforeItemId: undefined,
    });
  });

  it('Key_WhenAtTheEdgesOfThePage_ShouldMoveNothing', () => {
    expect(dropByKey(sections, id('SHIP-160'), 'up')).toBeNull();
    expect(dropByKey(sections, id('SHIP-172'), 'down')).toBeNull();
  });
});

describe('sectionOfDropId', () => {
  it('SectionOf_WhenAHeaderOrAHint_ShouldNameTheSection', () => {
    expect(sectionOfDropId(sectionDropId('sp26'))).toBe('sp26');
    expect(sectionOfDropId(sectionDropId('sp26', 'hint'))).toBe('sp26');
    expect(sectionOfDropId(sectionDropId('backlog'))).toBe('backlog');
  });

  it('SectionOf_WhenARow_ShouldBeUndefined', () => {
    expect(sectionOfDropId('id-SHIP-170')).toBeUndefined();
  });
});
