import { expect, test } from '@playwright/experimental-ct-react';
import type { RetroPointDto } from '../../../api/retros';
import { noteStates, stubRetros } from '../../retros/ct/retroFixtures';
import { retroSharedTestIds as ids } from '../testIds';
import { RetroNoteHarness } from './ct/RetroNoteHarness';

const withBadly = (point: RetroPointDto): RetroPointDto => ({ ...point, category: 'wentBadly' });

test('RetroNote_WhenUndecided_ShouldShowTheButtons', async ({ mount, page }) => {
  await stubRetros(page);
  const improve: RetroPointDto = { ...noteStates.undecided, category: 'improve' };
  await mount(<RetroNoteHarness point={improve} />);
  const add = page.getByTestId(ids.retroNoteAddAction);
  await expect(add).toHaveAttribute('data-variant', 'button');
  await expect(page.getByTestId(ids.retroNoteNotNeeded)).toBeVisible();
  await expect(page.getByTestId(ids.retroNotePin)).toHaveCount(0);
});

test('RetroNote_WhenWentBadlyAndUndecided_ShouldShowALinkRatherThanAButton', async ({
  mount,
  page,
}) => {
  await stubRetros(page);
  await mount(<RetroNoteHarness point={withBadly(noteStates.undecided)} />);
  await expect(page.getByTestId(ids.retroNoteAddAction)).toHaveAttribute('data-variant', 'link');
});

test('RetroNote_WhenActioned_ShouldShowThePin', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<RetroNoteHarness point={noteStates.actioned} />);
  const pin = page.getByTestId(ids.retroNotePin);
  await expect(pin).toContainText(noteStates.actioned.action!.key);
  await expect(page.getByTestId(ids.retroNoteAddAction)).toHaveCount(0);
  await expect(page.getByTestId(ids.retroNoteNone)).toHaveCount(0);
});

test('RetroNote_WhenNotNeeded_ShouldShowTheReason', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<RetroNoteHarness point={noteStates.notNeeded} />);
  await expect(page.getByTestId(ids.retroNoteNone)).toContainText(
    noteStates.notNeeded.notNeeded!.reason,
  );
  await expect(page.getByTestId(ids.retroNoteUndo)).toBeVisible();
  await expect(page.getByTestId(ids.retroNoteAddAction)).toHaveCount(0);
});

test('RetroNote_WhenWentWell_ShouldShowNoDecisionControls', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<RetroNoteHarness point={noteStates.wentWell} />);
  await expect(page.getByTestId(ids.retroNote)).toBeVisible();
  for (const id of [
    ids.retroNoteAddAction,
    ids.retroNoteNotNeeded,
    ids.retroNotePin,
    ids.retroNoteNone,
  ]) {
    await expect(page.getByTestId(id)).toHaveCount(0);
  }
});

test('RetroNote_WhenHighlighted_ShouldMarkAndScrollToIt', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<RetroNoteHarness point={noteStates.undecided} highlighted tall />);
  const note = page.getByTestId(ids.retroNote);
  await expect(note).toHaveAttribute('data-highlighted', 'true');
  await expect(note).toBeInViewport();
});
