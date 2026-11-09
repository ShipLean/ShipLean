import { expect, test } from 'vitest';
import type { UserRef } from '@shiplean/core/api/refs';
import { applyIssueIntakeSave, issueIntakeSample } from './issueIntakeStub';

const tom: UserRef = {
  id: 'tom-id',
  name: 'Tom Ng',
  initials: 'TN',
  avatarColour: '#112233',
  photoUrl: null,
};
const save = { enabled: true, proposalInstructions: '', maxRounds: 5 };

test('applyIssueIntakeSave_WhenPersonSelectedThenCleared_ShouldFollowRunAsAndReadiness', () => {
  const selected = applyIssueIntakeSave(
    {
      ...issueIntakeSample,
      runAs: null,
      readiness: { ...issueIntakeSample.readiness, hasRunAs: false },
    },
    { ...save, runAsUserId: tom.id },
    [tom],
  );
  expect(selected.runAs).toEqual(tom);
  expect(selected.readiness.hasRunAs).toBe(true);

  const cleared = applyIssueIntakeSave(selected, { ...save, runAsUserId: null }, [tom]);
  expect(cleared.runAs).toBeNull();
  expect(cleared.readiness.hasRunAs).toBe(false);
});
