import { expect, test } from 'vitest';
import instance from '../../../ShipLean.TestSupport/DtoFixtures/projects/instance.json';
import keyCheck from '../../../ShipLean.TestSupport/DtoFixtures/projects/key-check.json';
import members from '../../../ShipLean.TestSupport/DtoFixtures/projects/members.json';
import projectCreate from '../../../ShipLean.TestSupport/DtoFixtures/projects/project-create.json';
import project from '../../../ShipLean.TestSupport/DtoFixtures/projects/project.json';
import projectSettingsPut from '../../../ShipLean.TestSupport/DtoFixtures/projects/project-settings-put.json';
import projectSettings from '../../../ShipLean.TestSupport/DtoFixtures/projects/project-settings.json';
import projectSummaries from '../../../ShipLean.TestSupport/DtoFixtures/projects/project-summaries.json';
import stateChanges from '../../../ShipLean.TestSupport/DtoFixtures/projects/state-changes.json';
import states from '../../../ShipLean.TestSupport/DtoFixtures/projects/states.json';
import type {
  InstanceProjectDto,
  InstanceSettingsDto,
  KeyCheckDto,
  MemberDto,
  ProjectCreate,
  ProjectDto,
  ProjectSettingsDto,
  ProjectSettingsPut,
  ProjectSummaryDto,
  StateChanges,
  StateDto,
} from './projects';

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

test('projects_ShouldTypeTheFixtures', () => {
  expect(typed<ProjectSummaryDto[]>()(projectSummaries)).toHaveLength(3);
  expect(typed<ProjectDto>()(project).key).toBe('SHIP');
  expect(typed<ProjectSettingsDto>()(projectSettings).general.key).toBe('SHIP');
  expect(typed<ProjectSettingsPut>()(projectSettingsPut).members).toHaveLength(2);
  expect(typed<ProjectCreate>()(projectCreate).firstSprint).not.toBeNull();
  expect(typed<KeyCheckDto>()(keyCheck.taken).reason).toBe('takenByAnotherProject');
  expect(typed<KeyCheckDto>()(keyCheck.free).available).toBe(true);
  expect(typed<MemberDto[]>()(members)).toHaveLength(2);
  expect(typed<StateDto[]>()(states)).toHaveLength(3);
  expect(typed<StateChanges>()(stateChanges).remove).toHaveLength(1);
  expect(typed<InstanceSettingsDto>()(instance.settings).projectCreation).toBe('admins');
  expect(typed<InstanceProjectDto[]>()(instance.projects)[0].hasActiveAdmin).toBe(false);
});
