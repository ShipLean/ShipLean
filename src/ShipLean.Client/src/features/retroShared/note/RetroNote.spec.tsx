import { expect, test } from '@playwright/experimental-ct-react';
import type { RetroPointDto } from '../../../api/retros';
import { noteStates, stubRetros } from '../../retros/ct/retroFixtures';
import { retroSharedTestIds as ids } from '../testIds';
import { componentTestIds } from '../../../components/testIds';
import { RetroNoteHarness } from './ct/RetroNoteHarness';
import { RetroNoteStatesHarness } from './ct/RetroNoteStatesHarness';

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

const improve: RetroPointDto = { ...noteStates.undecided, category: 'improve' };

test('RetroNote_WhenReasonBlank_ShouldNotLetNotNeededBeSaved', async ({ mount, page }) => {
  const stub = await stubRetros(page);
  await mount(<RetroNoteHarness point={improve} />);
  await page.getByTestId(ids.retroNoteNotNeeded).click();
  const save = page.getByTestId(ids.retroNoteWhySave);
  await expect(save).toBeDisabled();
  await page.getByTestId(ids.retroNoteWhyText).fill('   ');
  await expect(save).toBeDisabled();
  await page.getByTestId(ids.retroNoteWhyText).fill('Covered elsewhere');
  await expect(save).toBeEnabled();
  expect(stub.writes()).toHaveLength(0);
});

test('RetroNote_WhenNotNeededSaved_ShouldCallTheServerAndShowTheBlock', async ({ mount, page }) => {
  const stub = await stubRetros(page);
  let answered = false;
  await page.route(/\/api\/retros\/points\/[^/]+\/not-needed$/, (route) => {
    answered = true;
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(noteStates.notNeeded),
    });
  });
  const component = await mount(<RetroNoteHarness point={improve} />);
  await page.getByTestId(ids.retroNoteNotNeeded).click();
  await page.getByTestId(ids.retroNoteWhyText).fill('Covered elsewhere');
  await page.getByTestId(ids.retroNoteWhySave).click();
  await expect.poll(() => answered).toBe(true);
  expect(stub.calls).toBeDefined();
  await component.update(<RetroNoteHarness point={noteStates.notNeeded} />);
  await expect(page.getByTestId(ids.retroNoteNone)).toContainText(
    noteStates.notNeeded.notNeeded!.reason,
  );
});

test('RetroNote_WhenUndoClicked_ShouldCallUndoAndOfferTheButtonsAgain', async ({ mount, page }) => {
  const stub = await stubRetros(page);
  const component = await mount(<RetroNoteHarness point={noteStates.notNeeded} />);
  await page.getByTestId(ids.retroNoteUndo).click();
  await expect
    .poll(() => stub.writes().map((w) => w.path.split('/').pop()))
    .toContain('undo-not-needed');
  await component.update(
    <RetroNoteHarness point={{ ...noteStates.undecided, category: 'improve' }} />,
  );
  await expect(page.getByTestId(ids.retroNoteAddAction)).toBeVisible();
  await expect(page.getByTestId(ids.retroNoteNone)).toHaveCount(0);
});

test('RetroNote_WhenTheServerRejectsTheWrite_ShouldShowAToast', async ({ mount, page }) => {
  await stubRetros(page);
  await page.route(/\/api\/retros\/points\/[^/]+\/not-needed$/, (route) =>
    route.fulfill({
      status: 400,
      contentType: 'application/json',
      body: JSON.stringify({ error: 'A reason is required.', code: 'validation' }),
    }),
  );
  await mount(<RetroNoteHarness point={improve} />);
  await page.getByTestId(ids.retroNoteNotNeeded).click();
  await page.getByTestId(ids.retroNoteWhyText).fill('x');
  await page.getByTestId(ids.retroNoteWhySave).click();
  await expect(page.getByTestId(componentTestIds.toast)).toContainText('A reason is required.');
});

test('RetroNote_WhenThemeLineUsed_ShouldPickAThemeAndCloseOnEscape', async ({ mount, page }) => {
  const stub = await stubRetros(page);
  await mount(<RetroNoteHarness point={improve} />);
  const line = page.getByTestId(ids.retroNoteTheme);
  await expect(line).toBeEnabled();
  await line.click();
  await expect(page.getByTestId(ids.retroThemePicker)).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(page.getByTestId(ids.retroThemePicker)).toHaveCount(0);
  await line.click();
  await page.getByRole('option').first().click();
  await expect(page.getByTestId(ids.retroThemePicker)).toHaveCount(0);
  await expect.poll(() => stub.writes().map((w) => w.path.split('/').pop())).toContain('theme');
});

test('RetroNote_WhenClickedOutsideThePicker_ShouldCloseIt', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<RetroNoteHarness point={improve} />);
  await expect(page.getByTestId(ids.retroNoteTheme)).toBeEnabled();
  await page.getByTestId(ids.retroNoteTheme).click();
  await expect(page.getByTestId(ids.retroThemePicker)).toBeVisible();
  await page.getByTestId(ids.retroNote).getByText(improve.text).click();
  await expect(page.getByTestId(ids.retroThemePicker)).toHaveCount(0);
});

test('RetroNote_WhenOwnPointOnly_ShouldOfferArchive', async ({ mount, page }) => {
  const stub = await stubRetros(page);
  const component = await mount(<RetroNoteHarness point={{ ...improve, canArchive: false }} />);
  await expect(page.getByTestId(ids.retroNoteMore)).toHaveCount(0);
  await component.update(<RetroNoteHarness point={{ ...improve, canArchive: true }} />);
  await page.getByTestId(ids.retroNoteMore).click();
  await page.getByTestId(ids.retroNoteArchive).click();
  await expect.poll(() => stub.writes().map((w) => w.path.split('/').pop())).toContain('archive');
});

test('RetroNote_WhenActionCoversOtherPoints_ShouldCountTheOthers', async ({ mount, page }) => {
  await stubRetros(page);
  const action = noteStates.actioned.action!;
  const component = await mount(
    <RetroNoteHarness point={{ ...noteStates.actioned, action: { ...action, pointCount: 3 } }} />,
  );
  await expect(page.getByTestId(ids.retroNotePin)).toContainText('+2 more points');
  await component.update(
    <RetroNoteHarness point={{ ...noteStates.actioned, action: { ...action, pointCount: 2 } }} />,
  );
  await expect(page.getByTestId(ids.retroNotePin)).toContainText('+1 more point');
  await expect(page.getByTestId(ids.retroNotePin)).not.toContainText('+1 more points');
  await component.update(
    <RetroNoteHarness point={{ ...noteStates.actioned, action: { ...action, pointCount: 1 } }} />,
  );
  await expect(page.getByTestId(ids.retroNotePin)).not.toContainText('more point');
});

test('RetroNote_WhenNotInPlan_ShouldShowALinkThatOpensTheDialogWithThePoint', async ({
  mount,
  page,
}) => {
  const stub = await stubRetros(page);
  const point: RetroPointDto = { ...noteStates.undecided, category: 'notInPlan' };
  await mount(<RetroNoteHarness point={point} />);
  const add = page.getByTestId(ids.retroNoteAddAction);
  await expect(add).toHaveAttribute('data-variant', 'link');
  await add.click();
  await expect(page.getByTestId(ids.retroActionDialog)).toBeVisible();
  await expect
    .poll(() => stub.reads('/retros/action-dialog').map((c) => c.query))
    .toContainEqual(expect.stringContaining(`point=${point.id}`));
});

test('RetroNote_WhenRendered_ShouldNotBeDraggable', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<RetroNoteHarness point={improve} />);
  const note = page.getByTestId(ids.retroNote);
  await expect(note).toBeVisible();
  expect(await note.evaluate((el) => el.getAttribute('draggable'))).toBeNull();
  expect(await note.locator('[draggable="true"]').count()).toBe(0);
  const dragged = await note.evaluate(async (el) => {
    let started = false;
    el.addEventListener('dragstart', () => (started = true));
    const box = el.getBoundingClientRect();
    el.dispatchEvent(
      new MouseEvent('mousedown', { bubbles: true, clientX: box.x + 5, clientY: box.y + 5 }),
    );
    return started || (el as HTMLElement).draggable;
  });
  expect(dragged).toBe(false);
});

test('RetroNote_WhenPaper_ShouldBePostitInBothThemes', async ({ mount, page }) => {
  await stubRetros(page);
  await mount(<RetroNoteHarness point={improve} />);
  for (const theme of ['light', 'dark']) {
    await page.evaluate((v) => document.documentElement.setAttribute('data-theme', v), theme);
    const style = await page.getByTestId(ids.retroNote).evaluate((el) => ({
      bg: getComputedStyle(el).backgroundColor,
      ink: getComputedStyle(el).color,
    }));
    expect(style).toEqual({ bg: 'rgb(255, 249, 211)', ink: 'rgb(28, 38, 56)' });
  }
});

for (const theme of ['light', 'dark'] as const) {
  test(`RetroNote_WhenEveryStateShown_ShouldMatchItsScreenshot_${theme}`, async ({
    mount,
    page,
  }) => {
    await stubRetros(page);
    await page.emulateMedia({ colorScheme: theme, reducedMotion: 'reduce' });
    await page.evaluate((v) => document.documentElement.setAttribute('data-theme', v), theme);
    const action = noteStates.actioned.action!;
    const own = { ...improve, id: 'own', canArchive: true, theme: null };
    const asking = { ...improve, id: 'asking', theme: null };
    const component = await mount(
      <RetroNoteStatesHarness
        points={[
          improve,
          {
            ...noteStates.undecided,
            id: 'badly',
            category: 'wentBadly',
            rootCause: 'The brief named it but no double.',
          },
          asking,
          { ...noteStates.actioned, action: { ...action, pointCount: 3 } },
          noteStates.notNeeded,
          noteStates.wentWell,
          own,
        ]}
      />,
    );
    await page.locator('[data-point-id="asking"]').getByTestId(ids.retroNoteNotNeeded).click();
    await expect(component.getByTestId('note-states')).toHaveScreenshot(
      `retro-note-states.${theme}.png`,
      {
        animations: 'disabled',
      },
    );
  });
}
