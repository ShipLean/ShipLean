import { expect, test } from 'vitest';
import connection from '../../../ShipLean.TestSupport/DtoFixtures/import/connection.json';
import errors from '../../../ShipLean.TestSupport/DtoFixtures/import/errors.json';
import jobs from '../../../ShipLean.TestSupport/DtoFixtures/import/jobs.json';
import mappingSuggested from '../../../ShipLean.TestSupport/DtoFixtures/import/mapping-suggested.json';
import mapping from '../../../ShipLean.TestSupport/DtoFixtures/import/mapping.json';
import projects from '../../../ShipLean.TestSupport/DtoFixtures/import/projects.json';
import startRequest from '../../../ShipLean.TestSupport/DtoFixtures/import/start-request.json';
import type {
  ImportErrorDto,
  ImportJobDto,
  ImportLogLineDto,
  ImportMappingDto,
  ImportStartRequest,
  JiraConnectionDto,
  JiraProjectDto,
  JiraTestResultDto,
} from './import';

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

test('import_ShouldTypeTheFixtures', () => {
  expect(typed<JiraConnectionDto>()(connection.connection).kind).toBe('cloud');
  expect(typed<JiraConnectionDto>()(connection.dataCenter).createdBy).toBeNull();
  expect(typed<JiraTestResultDto>()(connection.testOk).who?.displayName).toBe('Priya Shah');
  expect(typed<JiraTestResultDto>()(connection.testFailed).ok).toBe(false);
  expect(typed<JiraProjectDto[]>()(projects).filter((p) => p.importedAs !== null)).toHaveLength(1);
  expect(typed<ImportMappingDto>()(mapping).warnings).toHaveLength(4);
  expect(
    typed<ImportMappingDto>()(mappingSuggested).statuses.filter(
      (status) => status.isNewSinceLastRun,
    ),
  ).toHaveLength(1);
  expect(
    typed<ImportMappingDto>()(mappingSuggested).users.filter((user) => user.isNewSinceLastRun),
  ).toHaveLength(1);
  expect(typed<ImportStartRequest>()(startRequest).target.kind).toBe('newProject');
  expect(typed<ImportJobDto>()(jobs.running).progress.counts.comments.total).toBe(3912);
  expect(typed<ImportJobDto>()(jobs.finished).status).toBe('done');
  expect(typed<ImportJobDto>()(jobs.queuedRerun).progress.rerun).toBe(true);
  expect(typed<ImportErrorDto[]>()(errors.errors)).toHaveLength(3);
  expect(typed<ImportLogLineDto[]>()(errors.log)).toHaveLength(4);
});
