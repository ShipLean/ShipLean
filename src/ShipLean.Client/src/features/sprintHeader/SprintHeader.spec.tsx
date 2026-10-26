import { expect, test } from '@playwright/experimental-ct-react';
import type { ProjectDto } from '../../api/projects';
import type { SprintDto } from '../../api/sprints';
import { componentTestIds } from '../../components/testIds';
import { SprintGoalHarness } from './ct/SprintGoalHarness';
import { SprintHeaderHarness } from './ct/SprintHeaderHarness';
import { sprintHeaderHarnessIds as harness } from './ct/harnessIds';
import type { SprintHeaderProps } from './SprintHeader';
import { sprintHeaderTestIds as ids } from './testIds';

const project: ProjectDto = {
  id: 'p1',
  key: 'SHIP',
  name: 'Shipping',
  description: '',
  role: 'admin',
  memberCount: 3,
  workingDays: [1, 2, 3, 4, 5],
  velocityWindow: 3,
  velocityOverride: null,
  nextItemNumber: 1,
};

const sprintOf = (id: string, name: string, status: SprintDto['status']): SprintDto => ({
  id,
  name,
  startDate: '2026-10-19',
  endDate: '2026-10-30',
  goal: 'Check out with a saved card',
  status,
  rolloverStatus: null,
  datesLabel: '19 Oct – 30 Oct',
  timeLabel: '5 days left',
});

const current = sprintOf('s24', 'Sprint 24', 'current');
const past = sprintOf('s23', 'Sprint 23', 'past');
const sprints = [current, past];

const props = (over: Partial<SprintHeaderProps> = {}): SprintHeaderProps => ({
  variant: 'full',
  project,
  sprint: current,
  sprints,
  tab: 'backlog',
  ...over,
});

test('SprintHeader_WhenEnded_ShouldShowEndedChip', async ({ mount, page }) => {
  await mount(<SprintHeaderHarness {...props({ sprint: past })} />);
  await expect(page.getByTestId(componentTestIds.chip)).toHaveText('Ended');
  await expect(page.getByTestId(componentTestIds.chip)).toHaveAttribute('data-tone', 'muted');
  await expect(page.getByTestId(ids.dates)).toHaveText('19 Oct – 30 Oct');
});

test('SprintHeader_WhenCurrent_ShouldShowCurrentChip', async ({ mount, page }) => {
  await mount(<SprintHeaderHarness {...props()} />);
  await expect(page.getByTestId(componentTestIds.chip)).toHaveText('Current');
  await expect(page.getByTestId(componentTestIds.chip)).toHaveAttribute('data-tone', 'accent');
  await expect(page.getByTestId(ids.timeLeft)).toHaveText('5 days left');
  await expect(page.getByTestId(ids.tabBacklog)).toHaveAttribute('aria-current', 'page');
});

test('SprintHeader_WhenFuture_ShouldShowNoChip', async ({ mount, page }) => {
  await mount(
    <SprintHeaderHarness {...props({ sprint: sprintOf('s25', 'Sprint 25', 'future') })} />,
  );
  await expect(page.getByTestId(ids.dates)).toBeVisible();
  await expect(page.getByTestId(componentTestIds.chip)).toHaveCount(0);
});

test('SprintHeader_WhenSlim_ShouldHideTabs', async ({ mount, page }) => {
  await mount(<SprintHeaderHarness {...props({ variant: 'slim', tab: 'taskboard' })} />);
  await expect(page.getByTestId(ids.picker)).toBeVisible();
  await expect(page.getByTestId(ids.tabs)).toHaveCount(0);
  await expect(page.getByTestId(ids.goalEdit)).toHaveCount(0);
});

test('SprintHeader_WhenSprintPicked_ShouldOpenSameTab', async ({ mount, page }) => {
  await mount(<SprintHeaderHarness {...props({ tab: 'capacity' })} />, {
    hooksConfig: { route: '/p/SHIP/sprints/s24/capacity' },
  });
  await page.getByTestId(ids.picker).click();
  await page.getByTestId(ids.pickerOption).filter({ hasText: 'Sprint 23' }).click();
  await expect(page.getByTestId(harness.location)).toHaveText('/p/SHIP/sprints/s23/capacity');
});

test('SprintHeader_WhenEditClicked_ShouldRenderGoalSlot', async ({ mount, page }) => {
  await mount(<SprintHeaderHarness {...props()} />);
  await expect(page.getByTestId(ids.goal)).toHaveText('Check out with a saved card');
  await page.getByTestId(ids.goalEdit).click();
  await expect(page.getByTestId('slot-sprintGoalEdit')).toHaveCount(1);
  await expect(page.getByTestId(ids.goalEdit)).toHaveCount(0);
});

test('SprintHeader_WhenTabClicked_ShouldLinkToSprintTab', async ({ mount, page }) => {
  await mount(<SprintHeaderHarness {...props()} />, {
    hooksConfig: { route: '/p/SHIP/sprints/s24/backlog' },
  });
  await page.getByTestId(ids.tabTaskboard).click();
  await expect(page.getByTestId(harness.location)).toHaveText('/p/SHIP/sprints/s24/taskboard');
  await page.getByTestId(ids.allSprints).click();
  await expect(page.getByTestId(harness.location)).toHaveText('/p/SHIP/sprints');
});

test('SprintHeader_WhenCapacityTab_ShouldMarkOnlyCapacityCurrent', async ({ mount, page }) => {
  await mount(<SprintHeaderHarness {...props({ tab: 'capacity' })} />);
  await expect(page.getByTestId(ids.tabCapacity)).toHaveAttribute('aria-current', 'page');
  await expect(page.getByTestId(ids.tabBacklog)).not.toHaveAttribute('aria-current', 'page');
  await expect(page.getByTestId(ids.tabTaskboard)).not.toHaveAttribute('aria-current', 'page');
});

test('SprintHeader_WhenSprintChanges_ShouldLeaveGoalEditing', async ({ mount, page }) => {
  const component = await mount(<SprintHeaderHarness {...props()} />);
  await page.getByTestId(ids.goalEdit).click();
  await expect(page.getByTestId('slot-sprintGoalEdit')).toHaveCount(1);
  await component.update(<SprintHeaderHarness {...props({ sprint: past })} />);
  await expect(page.getByTestId(ids.goalEdit)).toBeVisible();
  await expect(page.getByTestId('slot-sprintGoalEdit')).toHaveCount(0);
});

test('SprintHeader_WhenEditorIsDone_ShouldCloseItAndFocusEdit', async ({ mount, page }) => {
  await mount(<SprintGoalHarness project={project} sprint={current} sprints={sprints} />);
  await page.getByTestId(ids.goalEdit).click();
  await expect(page.getByTestId(ids.goalEdit)).toHaveCount(0);
  await page.getByTestId(harness.editorDone).click();
  await expect(page.getByTestId(harness.editorDone)).toHaveCount(0);
  await expect(page.getByTestId(ids.goalEdit)).toBeFocused();
});

test('SprintHeader_WhenEditButtonRendered_ShouldBeNamedEditGoal', async ({ mount, page }) => {
  await mount(<SprintHeaderHarness {...props()} />);
  await expect(page.getByTestId(ids.goalEdit)).toHaveAttribute('aria-label', 'Edit goal');
});

test('SprintHeader_WhenPickedByKeyboard_ShouldNavigateOnlyOnEnter', async ({ mount, page }) => {
  await mount(<SprintHeaderHarness {...props()} />, {
    hooksConfig: { route: '/p/SHIP/sprints/s24/backlog' },
  });
  await page.getByTestId(ids.picker).click();
  await expect(page.getByTestId(ids.pickerList)).toBeFocused();
  await page.keyboard.press('ArrowDown');
  await expect(page.getByTestId(harness.location)).toHaveText('/p/SHIP/sprints/s24/backlog');
  await page.keyboard.press('Enter');
  await expect(page.getByTestId(harness.location)).toHaveText('/p/SHIP/sprints/s23/backlog');
  await expect(page.getByTestId(ids.pickerList)).toHaveCount(0);
});

test('SprintHeader_WhenPickerEscaped_ShouldCloseWithoutNavigating', async ({ mount, page }) => {
  await mount(<SprintHeaderHarness {...props()} />, {
    hooksConfig: { route: '/p/SHIP/sprints/s24/backlog' },
  });
  await page.getByTestId(ids.picker).click();
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('Escape');
  await expect(page.getByTestId(ids.pickerList)).toHaveCount(0);
  await expect(page.getByTestId(ids.picker)).toBeFocused();
  await expect(page.getByTestId(harness.location)).toHaveText('/p/SHIP/sprints/s24/backlog');
});
