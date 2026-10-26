import { describe, expect, it } from 'vitest';
import { task, VIEW } from '../ct/fixtures';
import { dropRequest, keyboardTarget, pickUpRequest } from './moveRequests';

const card = VIEW.rows[0].cells[0].tasks[0]; // SHIP-173, New, in the SHIP-141 row

describe('dropRequest', () => {
  it('dropRequest_WhenCardStaysInItsCell_ShouldBeNull', () => {
    expect(
      dropRequest(card, 'stories', 'id-SHIP-141', {
        stateId: 't-new',
        rowId: 'id-SHIP-141',
        beforeItemId: null,
      }),
    ).toBeNull();
  });

  it('dropRequest_WhenReorderedInItsCell_ShouldKeepTheRowNullAndNameTheCard', () => {
    expect(
      dropRequest(card, 'stories', 'id-SHIP-141', {
        stateId: 't-new',
        rowId: 'id-SHIP-141',
        beforeItemId: 'x',
      }),
    ).toMatchObject({ row: null, beforeItemId: 'x' });
  });

  it('dropRequest_WhenDroppedOnOtherRow_ShouldNameTheRow', () => {
    expect(
      dropRequest(card, 'stories', 'id-SHIP-141', {
        stateId: 't-new',
        rowId: 'id-SHIP-138',
        beforeItemId: null,
      }),
    ).toMatchObject({ row: { grouping: 'stories', target: 'id-SHIP-138' } });
  });

  it('dropRequest_WhenDroppedOnUnparentedOrUnassigned_ShouldSendANullTarget', () => {
    expect(
      dropRequest(card, 'stories', 'id-SHIP-141', {
        stateId: 't-new',
        rowId: 'unparented',
        beforeItemId: null,
      })?.row,
    ).toEqual({ grouping: 'stories', target: null });
    expect(
      dropRequest(card, 'people', 'u-tom', {
        stateId: 't-new',
        rowId: 'unassigned',
        beforeItemId: null,
      })?.row,
    ).toEqual({ grouping: 'people', target: null });
  });

  it('dropRequest_WhenGroupedByPeople_ShouldNeverSendTheCardItGoesAbove', () => {
    expect(
      dropRequest(card, 'people', 'unassigned', {
        stateId: 't-active',
        rowId: 'u-tom',
        beforeItemId: 'x',
      })?.beforeItemId,
    ).toBeNull();
  });

  it('dropRequest_WhenSent_ShouldCarryTheCardsRevision', () => {
    const stale = task('SHIP-1', card.state, { revision: 7 });
    expect(
      dropRequest(stale, 'stories', 'unparented', {
        stateId: 't-active',
        rowId: 'unparented',
        beforeItemId: null,
      })?.expectedRevision,
    ).toBe(7);
  });
});

describe('pickUpRequest', () => {
  it('pickUpRequest_WhenBuilt_ShouldAssignMeInThePeopleGroupingAndStartTheState', () => {
    expect(pickUpRequest(card, 't-active', 'u-priya')).toEqual({
      taskId: card.id,
      expectedRevision: 1,
      stateId: 't-active',
      row: { grouping: 'people', target: 'u-priya' },
      beforeItemId: null,
    });
  });
});

describe('keyboardTarget', () => {
  it('keyboardTarget_WhenArrowKeys_ShouldStepOneColumnOrRow', () => {
    expect(keyboardTarget(VIEW, 'stories', card, 'id-SHIP-141', 'ArrowRight')).toEqual({
      stateId: 't-active',
      rowId: 'id-SHIP-141',
      beforeItemId: null,
    });
    expect(keyboardTarget(VIEW, 'stories', card, 'id-SHIP-141', 'ArrowDown')?.rowId).toBe(
      'id-SHIP-138',
    );
    expect(keyboardTarget(VIEW, 'people', card, 'unassigned', 'ArrowDown')?.rowId).toBe('u-priya');
  });

  it('keyboardTarget_WhenAtAnEdge_ShouldBeNull', () => {
    expect(keyboardTarget(VIEW, 'stories', card, 'id-SHIP-141', 'ArrowLeft')).toBeNull();
    expect(keyboardTarget(VIEW, 'stories', card, 'id-SHIP-141', 'ArrowUp')).toBeNull();
    expect(keyboardTarget(VIEW, 'stories', card, 'unparented', 'ArrowDown')).toBeNull();
  });

  it('keyboardTarget_WhenOtherKey_ShouldBeNull', () => {
    expect(keyboardTarget(VIEW, 'stories', card, 'id-SHIP-141', 'a')).toBeNull();
  });
});
