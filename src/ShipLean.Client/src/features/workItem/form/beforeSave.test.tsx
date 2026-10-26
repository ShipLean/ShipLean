import { act, useEffect } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import {
  BeforeSaveContext,
  useBeforeSave,
  useBeforeSaveGate,
  type BeforeSaveResult,
} from './beforeSave';

type Gate = ReturnType<typeof useBeforeSaveGate>;

function Section({ answer }: { answer: BeforeSaveResult }) {
  useBeforeSave(() => answer);
  return null;
}

// The gate of the last render, read by the tests after each act().
const mounted: { gate: Gate | null } = { gate: null };
function Form({ answers }: { answers: BeforeSaveResult[] }) {
  const gate = useBeforeSaveGate();
  useEffect(() => {
    mounted.gate = gate;
  });
  return (
    <BeforeSaveContext.Provider value={gate.registry}>
      {answers.map((answer, index) => (
        <Section key={index} answer={answer} />
      ))}
    </BeforeSaveContext.Provider>
  );
}

let root: Root;
beforeEach(() => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  root = createRoot(document.createElement('div'));
});
afterEach(() => {
  act(() => root.unmount());
});

const mount = (answers: BeforeSaveResult[]) => act(() => root.render(<Form answers={answers} />));
const current = () => {
  if (mounted.gate === null) throw new Error('The form is not mounted.');
  return mounted.gate;
};

test('BeforeSave_WhenCheckAsksConfirm_ShouldHoldSaveUntilConfirmed', () => {
  mount([{ confirm: 'The description has an external image.' }]);
  const save = vi.fn();

  act(() => current().request(save));

  expect(save).not.toHaveBeenCalled();
  expect(current().pending).toEqual(['The description has an external image.']);

  act(() => current().confirm(save));

  expect(save).toHaveBeenCalledOnce();
  expect(current().pending).toBeNull();
});

test('BeforeSave_WhenConfirmCancelled_ShouldNotSave', () => {
  mount([{ confirm: 'Sure?' }]);
  const save = vi.fn();

  act(() => current().request(save));
  act(() => current().cancel());

  expect(save).not.toHaveBeenCalled();
  expect(current().pending).toBeNull();
});

test('BeforeSave_WhenNoCheckAsks_ShouldSaveAtOnce', () => {
  mount([null, null]);
  const save = vi.fn();

  act(() => current().request(save));

  expect(save).toHaveBeenCalledOnce();
  expect(current().pending).toBeNull();
});

test('BeforeSave_WhenSeveralChecksAsk_ShouldAskOnceWithEveryMessage', () => {
  mount([{ confirm: 'First' }, null, { confirm: 'Second' }]);
  const save = vi.fn();

  act(() => current().request(save));

  expect(current().pending).toEqual(['First', 'Second']);
  expect(save).not.toHaveBeenCalled();
});

test('BeforeSave_WhenSectionUnmounted_ShouldStopAsking', () => {
  mount([{ confirm: 'Gone soon' }]);
  mount([]);
  const save = vi.fn();

  act(() => current().request(save));

  expect(save).toHaveBeenCalledOnce();
});
