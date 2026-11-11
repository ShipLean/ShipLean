import { expect, test } from 'vitest';
import {
  answered,
  empty,
  feature,
  featureChildrenOnly,
  other,
  settled,
  waiting,
} from './ct/fixtures';
import { asksState } from './regions';

test('asksState_WhenAQuestionOrDecisionIsOpen_ShouldBeWaiting', () => {
  expect(asksState(waiting)).toBe('waiting');
  expect(asksState(other)).toBe('waiting');
});

test('asksState_WhenOnlyADecisionIsOpen_ShouldBeWaiting', () => {
  const onlyDecision = { ...waiting, questions: [] };
  expect(asksState(onlyDecision)).toBe('waiting');
});

test('asksState_WhenOnlyAQuestionIsOpen_ShouldBeWaiting', () => {
  const onlyQuestion = { ...waiting, decisions: [] };
  expect(asksState(onlyQuestion)).toBe('waiting');
});

test('asksState_WhenEverythingIsSettled_ShouldBeSettled', () => {
  expect(asksState(settled)).toBe('settled');
  expect(asksState(answered)).toBe('settled');
});

test('asksState_WhenNothingIsAsked_ShouldBeEmpty', () => {
  expect(asksState(empty)).toBe('empty');
});

test('asksState_WhenOnlyAFeaturesChildrenWait_ShouldBeWaiting', () => {
  expect(asksState(featureChildrenOnly)).toBe('waiting');
  expect(asksState(feature)).toBe('waiting');
});

test('asksState_WhenAFeatureHasSettledAsksAndNoWaitingChildren_ShouldBeSettled', () => {
  expect(asksState({ ...feature, waitingChildren: [] })).toBe('settled');
});
