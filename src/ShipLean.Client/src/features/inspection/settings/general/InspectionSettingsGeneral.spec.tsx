import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import type { InspectionProjectSettingsDto } from '@shiplean/core/api/inspection';
import type { ProjectDto } from '@shiplean/core/api/projects';
import { inspectionSettings } from '../../ct/inspectionMocks';
import { InspectionSettingsGeneral } from '../../InspectionSettingsGeneral';
import { inspectionTestIds as ids } from '../../testIds';

const admin = { id: 'p-ship', key: 'SHIP', role: 'admin' } as ProjectDto;
const member = { id: 'p-ship', key: 'SHIP', role: 'member' } as ProjectDto;

const off: InspectionProjectSettingsDto = {
  ...inspectionSettings,
  enabled: true,
  showContent: false,
  tenantOverride: null,
  selectorOverride: null,
  storeMode: 'tempoLoki',
};

async function stub(page: Page, settings: InspectionProjectSettingsDto) {
  const puts: unknown[] = [];
  await page.route('**/api/inspection/projects/*/settings', (route) => {
    if (route.request().method() === 'PUT') {
      const body = route.request().postDataJSON() as Record<string, unknown>;
      puts.push(body);
      return route.fulfill({ json: { ...settings, ...body } });
    }
    return route.fulfill({ json: settings });
  });
  return puts;
}

test('General_WhenShowContentIsTurnedOn_ShouldWarnAndHoldSaveUntilConfirmed', async ({
  mount,
  page,
}) => {
  const puts = await stub(page, off);
  const view = await mount(<InspectionSettingsGeneral project={admin} />);
  await expect(view.getByTestId(ids.inspectionContentWarning)).toHaveCount(0);

  await view.getByTestId(ids.inspectionShowContent).check();
  await expect(view.getByTestId(ids.inspectionContentWarning)).toContainText('secrets');
  await expect(view.getByTestId(ids.inspectionSettingsSave)).toBeDisabled();

  await view.getByTestId(ids.inspectionContentConfirm).check();
  await view.getByTestId(ids.inspectionSettingsSave).click();
  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0]).toMatchObject({ enabled: true, showContent: true });
});

test('General_InTempoLokiMode_ShouldSendTrimmedOverridesAndBlankAsNull', async ({
  mount,
  page,
}) => {
  const puts = await stub(page, off);
  const view = await mount(<InspectionSettingsGeneral project={admin} />);
  await view.getByTestId(ids.inspectionTenantOverride).fill('  team-a ');
  await view.getByTestId(ids.inspectionSettingsSave).click();
  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0]).toEqual({
    enabled: true,
    showContent: false,
    tenantOverride: 'team-a',
    selectorOverride: null,
  });
});

test('General_InClickHouseMode_ShouldNotOfferOverrides', async ({ mount, page }) => {
  await stub(page, { ...off, storeMode: 'clickhouse' });
  const view = await mount(<InspectionSettingsGeneral project={admin} />);
  await expect(view.getByTestId(ids.inspectionStoreMode)).toHaveText('ClickHouse');
  await expect(view.getByTestId(ids.inspectionTenantOverride)).toHaveCount(0);
  await expect(view.getByTestId(ids.inspectionSelectorOverride)).toHaveCount(0);
});

test('General_ForAMember_ShouldBeReadOnlyWithNoSave', async ({ mount, page }) => {
  await stub(page, { ...off, showContent: true });
  const view = await mount(<InspectionSettingsGeneral project={member} />);
  await expect(view.getByTestId(ids.inspectionReadOnly)).toBeVisible();
  await expect(view.getByTestId(ids.inspectionEnabled)).toBeDisabled();
  await expect(view.getByTestId(ids.inspectionShowContent)).toBeDisabled();
  await expect(view.getByTestId(ids.inspectionTenantOverride)).toBeDisabled();
  await expect(view.getByTestId(ids.inspectionSettingsSave)).toHaveCount(0);
});

test('General_WhenSaveIsRefused_ShouldShowTheError', async ({ mount, page }) => {
  await page.route('**/api/inspection/projects/*/settings', (route) =>
    route.request().method() === 'PUT'
      ? route.fulfill({ status: 403, json: { error: 'Admins only.', code: 'forbidden' } })
      : route.fulfill({ json: off }),
  );
  const view = await mount(<InspectionSettingsGeneral project={admin} />);
  await view.getByTestId(ids.inspectionSettingsSave).click();
  await expect(view.getByTestId(ids.inspectionSettingsSaveError)).toContainText('Admins only');
});
