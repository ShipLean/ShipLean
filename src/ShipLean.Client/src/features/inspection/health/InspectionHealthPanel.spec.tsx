import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import type { InspectionHealthDto } from '@shiplean/core/api/inspection';
import { inspectionHealth } from '../ct/inspectionMocks';
import { inspectionTestIds as ids } from '../testIds';
import { InspectionHealthPanel } from './InspectionHealthPanel';

const stub = (page: Page, status: number, json: unknown) =>
  page.route('**/api/inspection/health', (route) => route.fulfill({ status, json }));

test('Health_ShouldShowModeReachabilityLatencyAndLag', async ({ mount, page }) => {
  await stub(page, 200, inspectionHealth);
  const view = await mount(<InspectionHealthPanel />);
  await expect(view.getByTestId(ids.inspectionHealthMode)).toHaveText('ClickHouse');
  await expect(view.getByTestId(ids.inspectionHealthReachable)).toHaveText('Yes');
  await expect(view.getByTestId(ids.inspectionHealthLatency)).toHaveText('12 ms');
  await expect(view.getByTestId(ids.inspectionHealthLag)).toHaveText('3 s');
  await expect(view.getByTestId(ids.inspectionHealthField)).toHaveCount(0);
});

test('Health_CheckFields_ShouldListAMissingFieldAsNotFound', async ({ mount, page }) => {
  await stub(page, 200, inspectionHealth);
  const view = await mount(<InspectionHealthPanel />);
  await view.getByTestId(ids.inspectionHealthCheckFields).click();
  const rows = view.getByTestId(ids.inspectionHealthField);
  await expect(rows).toHaveCount(3);
  await expect(rows.nth(0)).toContainText('found');
  await expect(rows.nth(0)).toHaveAttribute('data-found', 'true');
  await expect(rows.nth(2)).toContainText('vcs.ref.head.name');
  await expect(rows.nth(2)).toContainText('not found');
  await expect(rows.nth(2)).toHaveAttribute('data-found', 'false');
});

test('Health_WhenTheStoreIsDown_ShouldShowTheProblemAndNoLatency', async ({ mount, page }) => {
  const down: InspectionHealthDto = {
    ...inspectionHealth,
    reachable: false,
    latencyMs: null,
    viewLagSeconds: null,
    problem: 'Connection refused',
  };
  await stub(page, 200, down);
  const view = await mount(<InspectionHealthPanel />);
  await expect(view.getByTestId(ids.inspectionHealthReachable)).toHaveText('No');
  await expect(view.getByTestId(ids.inspectionHealthProblem)).toHaveText('Connection refused');
  await expect(view.getByTestId(ids.inspectionHealthLatency)).toHaveCount(0);
});

test('Health_WhenInspectionIsOffOnTheInstance_ShouldSaySo', async ({ mount, page }) => {
  await stub(page, 404, { error: 'Not found.', code: 'not_found' });
  const view = await mount(<InspectionHealthPanel />);
  await expect(view.getByTestId(ids.inspectionHealthOff)).toBeVisible();
});
