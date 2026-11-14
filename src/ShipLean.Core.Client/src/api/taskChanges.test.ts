import { expect, test } from 'vitest';
import combined from '../../../ShipLean.TestSupport/DtoFixtures/taskChanges/changes-combined.json';
import noAccess from '../../../ShipLean.TestSupport/DtoFixtures/taskChanges/changes-noaccess.json';
import perCommit from '../../../ShipLean.TestSupport/DtoFixtures/taskChanges/changes-per-commit.json';
import comment from '../../../ShipLean.TestSupport/DtoFixtures/taskChanges/comment.json';
import outcomes from '../../../ShipLean.TestSupport/DtoFixtures/taskChanges/review-outcome.json';
import section from '../../../ShipLean.TestSupport/DtoFixtures/taskChanges/section.json';
import {
  CHANGES_MODES,
  CODE_SIDES,
  COMMIT_ACCESS,
  COMMIT_PROVIDERS,
  DIFF_FILE_STATUSES,
  type CodeCommentDto,
  type ReviewOutcomeDto,
  type TaskChangesDto,
  type TaskCommitsSectionDto,
} from './taskChanges';

// Typecheck fails if a fixture lacks a field, has one of the wrong type, or has one the type doesn't declare.
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

type NoExtraKeys<U, T> = U extends readonly (infer UE)[]
  ? T extends readonly (infer TE)[]
    ? NoExtraKeys<UE, TE>[]
    : U
  : U extends object
    ? T extends object
      ? { [K in keyof U]: K extends keyof T ? NoExtraKeys<U[K], T[K]> : never }
      : U
    : U;

function typed<T>() {
  return <U extends DeepWiden<T>>(value: U & NoExtraKeys<U, DeepWiden<T>>): DeepWiden<T> => value;
}

test('taskChanges_ShouldTypeEveryFixture', () => {
  const pages = [
    typed<TaskChangesDto>()(combined.changesCombined),
    typed<TaskChangesDto>()(perCommit.changesPerCommit),
    typed<TaskChangesDto>()(noAccess.changesNoAccess),
  ];
  for (const page of pages) {
    expect(CHANGES_MODES).toContain(page.mode);
    for (const row of page.commits) {
      expect(COMMIT_PROVIDERS).toContain(row.provider);
      expect(COMMIT_ACCESS).toContain(row.access);
    }
    for (const file of page.parts.flatMap((part) => part.files)) {
      expect(DIFF_FILE_STATUSES).toContain(file.status);
    }
  }
  expect(pages.some((page) => page.commits.some((row) => row.access !== 'ok'))).toBe(true);
});

test('taskCommitsSection_ShouldTypeItsFixture', () => {
  const value = typed<TaskCommitsSectionDto>()(section.section);
  expect(value.filePaths.length).toBeGreaterThan(0);
});

test('codeComment_ShouldTypeItsFixture', () => {
  const value = typed<CodeCommentDto>()(comment.comment);
  if (value.side !== null) expect(CODE_SIDES).toContain(value.side);
});

test('reviewOutcome_ShouldTypeBothFixtures', () => {
  typed<ReviewOutcomeDto>()(outcomes.approved);
  typed<ReviewOutcomeDto>()(outcomes.requested);
});
