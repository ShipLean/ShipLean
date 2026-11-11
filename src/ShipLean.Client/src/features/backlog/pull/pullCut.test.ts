import { expect, test } from 'vitest';
import { BULK_MAX_ROWS } from '@shiplean/core/api/backlogWrites';
import cases from '../../../../../ShipLean.TestSupport/DtoFixtures/backlog/pull-cut-cases.json';
import { pullCut } from './pullCut';

interface Case {
  name: string;
  stack?: (number | null)[];
  rows?: number;
  pointsEach?: number;
  room: number;
  alreadyIn: number;
  expect: {
    selected: number | 'cap';
    sprintPoints: number;
    overBy: number | null;
    capped: boolean;
  };
}

test.each(cases.cases as Case[])('Cut_$name', (c) => {
  const stack = c.stack ?? Array.from({ length: c.rows ?? 0 }, () => c.pointsEach ?? 0);
  const got = pullCut(stack, c.room, c.alreadyIn);
  expect(got).toEqual({
    ...c.expect,
    selected: c.expect.selected === 'cap' ? BULK_MAX_ROWS : c.expect.selected,
  });
});
