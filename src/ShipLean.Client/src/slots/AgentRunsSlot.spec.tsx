import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import type { ProjectDto } from '@shiplean/core/api/projects';
import story from '../../../ShipLean.TestSupport/DtoFixtures/workItems/work-item.story.json' with { type: 'json' };
import { stubInspection } from '../features/inspection/ct/inspectionMocks';
import { inspectionTestIds as ids } from '../features/inspection/testIds';
import { AgentRunsSlot } from './AgentRunsSlot';

const project = { id: 'p1', key: 'SHIP', role: 'member' } as ProjectDto;

async function stubItem(page: Page, inspectionEnabled: boolean) {
  await page.route('**/work-items/by-key/*', (route) =>
    route.fulfill({ json: { ...story, inspectionEnabled } }),
  );
  return stubInspection(page);
}

test('Slot_WhenFlagOnAtDesktopWidth_ShouldShowTheSection', async ({ mount, page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  const requests = await stubItem(page, true);
  await mount(<AgentRunsSlot project={project} itemKey="SHIP-1" />);

  await expect(page.getByTestId(ids.agentRunsSection)).toBeAttached();
  await expect.poll(() => requests.urls.some((u) => u.endsWith('/items/SHIP-1/runs'))).toBe(true);
});

test('Slot_WhenWidthIs899_ShouldShowNothingAndRequestNothing', async ({ mount, page }) => {
  await page.setViewportSize({ width: 899, height: 900 });
  const requests = await stubItem(page, true);
  await mount(<AgentRunsSlot project={project} itemKey="SHIP-1" />);

  await expect(page.getByTestId(ids.agentRunsSlot)).toBeAttached();
  await expect(page.getByTestId(ids.agentRunsSection)).toHaveCount(0);
  expect(requests.urls).toEqual([]);
});

test('Slot_WhenWidthIs900_ShouldShowTheSection', async ({ mount, page }) => {
  await page.setViewportSize({ width: 900, height: 900 });
  await stubItem(page, true);
  await mount(<AgentRunsSlot project={project} itemKey="SHIP-1" />);

  await expect(page.getByTestId(ids.agentRunsSection)).toBeAttached();
});

test('Slot_WhenFlagOff_ShouldShowNothingAndRequestNothing', async ({ mount, page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  const requests = await stubItem(page, false);
  await mount(<AgentRunsSlot project={project} itemKey="SHIP-1" />);

  await expect(page.getByTestId(ids.agentRunsSlot)).toBeAttached();
  await expect(page.getByTestId(ids.agentRunsSection)).toHaveCount(0);
  await page.waitForTimeout(300);
  expect(requests.urls).toEqual([]);
});

test('Slot_WhenFlagOff_ShouldTakeNoSpaceInTheLayout', async ({ mount, page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await stubItem(page, false);
  await mount(<AgentRunsSlot project={project} itemKey="SHIP-1" />);

  const slot = page.getByTestId(ids.agentRunsSlot);
  await expect(slot).toHaveCSS('display', 'contents');
  await expect(slot.locator('*')).toHaveCount(0);
});
