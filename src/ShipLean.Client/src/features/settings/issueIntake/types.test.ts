import { expect, test } from 'vitest';
import fixture from '../../../../../ShipLean.TestSupport/DtoFixtures/issueIntake/issue-intake-settings.json';
import { apiErrorCodes } from '../../../api/client';
import { issueIntakeErrorCodes, type IssueIntakeSettingsDto } from './types';

// Typecheck fails if the pinned GET body (the same file the HTTP shape test compares with) lacks a field, has one of the
// wrong type, or has one the type doesn't declare.
type DeepWiden<T> = T extends string
  ? string
  : T extends number
    ? number
    : T extends boolean
      ? boolean
      : T extends readonly (infer U)[]
        ? DeepWiden<U>[]
        : T extends object
          ? { [K in keyof T]: DeepWiden<T[K]> }
          : T;

type NoExtraKeys<U, T> = U extends object
  ? T extends object
    ? { [K in keyof U]: K extends keyof T ? NoExtraKeys<U[K], NonNullable<T[K]>> : never }
    : U
  : U;

function typed<T>() {
  return <U extends DeepWiden<T>>(value: U & NoExtraKeys<U, DeepWiden<T>>): DeepWiden<T> => value;
}

test('IssueIntakeSettingsDto_ShouldMatchThePinnedBody', () => {
  const settings: IssueIntakeSettingsDto = typed<IssueIntakeSettingsDto>()(fixture);

  expect(Object.keys(settings).sort()).toEqual(
    [
      'defaultProposalInstructions',
      'enabled',
      'maxRounds',
      'proposalInstructions',
      'readiness',
      'repository',
      'runAs',
    ].sort(),
  );
  expect(Object.keys(settings.readiness).sort()).toEqual(
    [
      'hasAgentConnection',
      'hasGitHubRepository',
      'hasGitHubToken',
      'hasRunAs',
      'hasWebhookSecret',
    ].sort(),
  );
});

test('IssueIntakeErrorCodes_ShouldBeKnownApiErrorCodes', () => {
  expect([...issueIntakeErrorCodes]).toEqual([
    'repository_taken',
    'no_repo_url',
    'not_member',
    'intake_on',
  ]);
  for (const code of issueIntakeErrorCodes) {
    expect(apiErrorCodes).toContain(code);
  }
});
