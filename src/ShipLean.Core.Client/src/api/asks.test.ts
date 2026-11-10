import { expect, test } from 'vitest';
import history from '../../../ShipLean.TestSupport/DtoFixtures/asks/history-asks.json';
import feature from '../../../ShipLean.TestSupport/DtoFixtures/asks/item-asks.feature.json';
import story from '../../../ShipLean.TestSupport/DtoFixtures/asks/item-asks.story.json';
import waiting from '../../../ShipLean.TestSupport/DtoFixtures/asks/waiting.json';
import {
  ANSWER_KINDS,
  ASK_EVENT_KINDS,
  DECISION_STATES,
  QUESTION_STATES,
  type ItemAsksDto,
  type WaitingDto,
} from './asks';
import type { HistoryDto } from './workItems';

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

const sorted = (values: Iterable<string | null>) =>
  [...new Set(values)].filter((v) => v !== null).sort();

test('asks_ShouldTypeEveryFixture', () => {
  const items = [typed<ItemAsksDto>()(story), typed<ItemAsksDto>()(feature)];
  const lines = typed<HistoryDto>()(history);
  const waitingOnMe = typed<WaitingDto>()(waiting);

  expect(items[0].decisions.map((d) => d.label)).toEqual([
    'D1',
    'D2',
    'D3',
    'D4',
    'D5',
    'D6',
    'D7',
  ]);
  expect(items[1].waitingChildren.filter((c) => c.ownerIsCreator)).toHaveLength(1);
  expect(lines.asks.length).toBeGreaterThan(0);
  expect(waitingOnMe.items.length).toBeGreaterThan(0);
});

test('asks_ShouldCoverEveryWireEnumValue', () => {
  const items = [typed<ItemAsksDto>()(story), typed<ItemAsksDto>()(feature)];
  const decisions = items.flatMap((i) => i.decisions);
  const events = decisions.flatMap((d) => d.history);
  const lines = typed<HistoryDto>()(history).asks;

  expect(sorted(items.flatMap((i) => i.questions).map((q) => q.state))).toEqual(
    [...QUESTION_STATES].sort(),
  );
  expect(sorted(decisions.map((d) => d.state))).toEqual([...DECISION_STATES].sort());
  expect(sorted([...decisions.map((d) => d.answerKind), ...events.map((e) => e.toKind)])).toEqual(
    [...ANSWER_KINDS].sort(),
  );
  expect(sorted([...lines.map((l) => l.kind), ...events.map((e) => e.kind)])).toEqual(
    [...ASK_EVENT_KINDS].sort(),
  );
});
