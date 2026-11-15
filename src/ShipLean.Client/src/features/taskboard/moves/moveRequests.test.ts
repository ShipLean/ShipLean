import { describe, expect, it } from 'vitest';
import { task, VIEW } from '../ct/fixtures';
import {
  assignRequest,
  dropRequest,
  keyboardTarget,
  pickUpRequest,
  reparentRequest,
} from './moveRequests';

const card = VIEW.rows[0].cells[0].tasks[0]; // SHIP-173, New, in the SHIP-141 row

describe('dropRequest', () => {
  it('dropRequest_WhenCardStaysInItsCell_ShouldBeNull', () => {
    expect(
      dropRequest(
        card,
        'stories',
        'id-SHIP-141',
        {
          stateId: 't-new',
          rowId: 'id-SHIP-141',
          beforeItemId: null,
        },
        'drag',
      ),
    ).toBeNull();
  });

  it('dropRequest_WhenReorderedInItsCell_ShouldKeepTheRowNullAndNameTheCard', () => {
    expect(
      dropRequest(
        card,
        'stories',
        'id-SHIP-141',
        {
          stateId: 't-new',
          rowId: 'id-SHIP-141',
          beforeItemId: 'x',
        },
        'drag',
      ),
    ).toMatchObject({ row: null, beforeItemId: 'x' });
  });

  it('dropRequest_WhenDroppedOnOtherRow_ShouldNameTheRow', () => {
    expect(
      dropRequest(
        card,
        'stories',
        'id-SHIP-141',
        {
          stateId: 't-new',
          rowId: 'id-SHIP-138',
          beforeItemId: null,
        },
        'drag',
      ),
    ).toMatchObject({ row: { grouping: 'stories', target: 'id-SHIP-138' } });
  });

  it('dropRequest_WhenDroppedOnUnparentedOrUnassigned_ShouldSendANullTarget', () => {
    expect(
      dropRequest(
        card,
        'stories',
        'id-SHIP-141',
        {
          stateId: 't-new',
          rowId: 'unparented',
          beforeItemId: null,
        },
        'drag',
      )?.row,
    ).toEqual({ grouping: 'stories', target: null });
    expect(
      dropRequest(
        card,
        'people',
        'u-tom',
        {
          stateId: 't-new',
          rowId: 'unassigned',
          beforeItemId: null,
        },
        'drag',
      )?.row,
    ).toEqual({ grouping: 'people', target: null });
  });

  it('dropRequest_WhenGroupedByPeople_ShouldNeverSendTheCardItGoesAbove', () => {
    expect(
      dropRequest(
        card,
        'people',
        'unassigned',
        {
          stateId: 't-active',
          rowId: 'u-tom',
          beforeItemId: 'x',
        },
        'drag',
      )?.beforeItemId,
    ).toBeNull();
  });

  it('dropRequest_WhenSent_ShouldCarryTheCardsRevision', () => {
    const stale = task('SHIP-1', card.state, { revision: 7 });
    expect(
      dropRequest(
        stale,
        'stories',
        'unparented',
        {
          stateId: 't-active',
          rowId: 'unparented',
          beforeItemId: null,
        },
        'drag',
      )?.expectedRevision,
    ).toBe(7);
  });
});

describe('methods', () => {
  it('dropRequest_WhenGivenAMethod_ShouldCarryIt', () => {
    const to = { stateId: 't-active', rowId: 'id-SHIP-141', beforeItemId: null };
    expect(dropRequest(card, 'stories', 'id-SHIP-141', to, 'sheet')?.method).toBe('sheet');
    expect(dropRequest(card, 'stories', 'id-SHIP-141', to, 'keyboard')?.method).toBe('keyboard');
  });

  it('reparentRequest_WhenGivenAStory_ShouldKeepTheStateAndSendTheMethod', () => {
    expect(reparentRequest(card, 'id-SHIP-138', 'sheet')).toEqual({
      taskId: card.id,
      expectedRevision: card.revision,
      stateId: card.state.id,
      row: { grouping: 'stories', target: 'id-SHIP-138' },
      beforeItemId: null,
      method: 'sheet',
    });
  });

  it('assignRequest_WhenUnassigning_ShouldSendANullPeopleTarget', () => {
    expect(assignRequest(card, null, 'sheet')).toMatchObject({
      stateId: card.state.id,
      row: { grouping: 'people', target: null },
      method: 'sheet',
    });
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
