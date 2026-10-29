import { expect, test } from 'vitest';
import capacity from '../../../ShipLean.TestSupport/DtoFixtures/sprints/capacity.json';
import forecast from '../../../ShipLean.TestSupport/DtoFixtures/sprints/forecast.json';
import metrics from '../../../ShipLean.TestSupport/DtoFixtures/sprints/metrics.json';
import rollover from '../../../ShipLean.TestSupport/DtoFixtures/sprints/rollover.json';
import sprints from '../../../ShipLean.TestSupport/DtoFixtures/sprints/sprints.json';
import velocity from '../../../ShipLean.TestSupport/DtoFixtures/sprints/velocity.json';
import type {
  ForecastDto,
  RolloverDto,
  RolloverRequest,
  SprintCapacityDto,
  SprintCapacityPut,
  SprintCreate,
  SprintDto,
  SprintMetricsDto,
  SprintUpdate,
  VelocityDto,
} from './sprints';

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

test('sprints_ShouldTypeTheFixtures', () => {
  expect(typed<SprintDto[]>()(sprints).map((sprint) => sprint.status)).toEqual([
    'past',
    'current',
    'future',
  ]);
  expect(typed<SprintCapacityDto>()(capacity.capacity).teamTotal).toBe(21);
  expect(typed<SprintCapacityPut>()(capacity.put).members).toHaveLength(2);
  expect(typed<SprintMetricsDto>()(metrics).burndown).toHaveLength(3);
  expect(typed<VelocityDto>()(velocity).average).toBe(21);
  expect(typed<ForecastDto>()(forecast).sprints).toHaveLength(3);
  expect(typed<RolloverDto>()(rollover.rollover).items).toHaveLength(2);
  expect(typed<RolloverRequest>()(rollover.request).moves).toHaveLength(2);
  expect(typed<SprintCreate>()(rollover.create).name).toBe('Sprint 27');
  expect(typed<SprintUpdate>()(rollover.update).goal).toBe('Ship it');
});
