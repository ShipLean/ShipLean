import { expect } from '@playwright/experimental-ct-react';
import { promptsTestIds as ids } from '../../features/prompts/testIds';
import { projectSettings } from './fixtures/workItemSettings';
import fx from './fixtures/waysOfWorking.fixture.json' with { type: 'json' };
import { stubShell } from './shellStub';
import { pageShots, type Page } from './shoot';

// Ways of working (design/screens/prompts*.png, states from design/screenshots.sh): the stage strip, the prompt full
// width, and each tab, dialog and editor state. Driven by fixtures/waysOfWorking.fixture.json, so a shot differs from
// its mock-up only in UI. Baselines are regenerated in the CI image by scripts/update-page-shots.sh.
const ROUTE = '/p/SHIP/ways-of-working';

async function arrange(page: Page, canEdit = true) {
  await stubShell(page);
  await page.route('**/api/projects/p1/settings', (r) => r.fulfill({ json: projectSettings }));
  await page.route('**/api/projects/*/prompts', (r) =>
    r.fulfill({ json: { ...fx.page, canEdit } }),
  );
  await page.route('**/api/projects/*/prompts/*/history', (r) => r.fulfill({ json: fx.history }));
  await page.route('**/api/projects/*/prompts/*/assembled**', (r) =>
    r.fulfill({ json: fx.assembled }),
  );
  await page.route('**/api/projects/*/usage/**', (r) => r.fulfill({ status: 204 }));
  await page.route('**/api/**/markdown**', (r) =>
    r.fulfill({ json: { html: '<p>(rendered)</p>' } }),
  );
}

// Every shot waits for the page and the open prompt before its act step runs.
async function ready(page: Page) {
  await expect(page.getByTestId(ids.header)).toBeVisible();
  await expect(page.getByTestId(ids.detailName)).toBeVisible();
}

pageShots([
  {
    name: 'prompts',
    route: `${ROUTE}?prompt=guide`,
    shell: true,
    arrange: (p) => arrange(p),
    ready: async (p) => {
      await ready(p);
      await expect(p.getByTestId(ids.stageNav)).toBeVisible();
    },
  },
  {
    name: 'prompts-custom',
    route: `${ROUTE}?prompt=brief`,
    shell: true,
    arrange: (p) => arrange(p),
    ready,
    act: async (p) => {
      await p.getByTestId(ids.tabYourChanges).click();
      await expect(p.getByTestId(ids.panelYourChanges)).toBeVisible();
    },
  },
  {
    name: 'prompts-history',
    route: `${ROUTE}?prompt=brief`,
    shell: true,
    arrange: (p) => arrange(p),
    ready,
    act: async (p) => {
      await p.getByTestId(ids.tabHistory).click();
      await expect(p.getByTestId(ids.historyTable)).toBeVisible();
    },
  },
  {
    name: 'prompts-assembled',
    route: `${ROUTE}?prompt=brief`,
    shell: true,
    arrange: (p) => arrange(p),
    ready,
    act: async (p) => {
      await p.getByTestId(ids.tabAssembled).click();
      await expect(p.getByTestId(ids.assembledLayers)).toBeVisible();
    },
  },
  {
    name: 'prompts-merge',
    route: `${ROUTE}?prompt=codeReviewer`,
    shell: true,
    arrange: (p) => arrange(p),
    ready,
    act: async (p) => {
      await p.getByTestId(ids.tabUpdate).click();
      await expect(p.getByTestId(ids.panelUpdate)).toBeVisible();
    },
  },
  {
    name: 'prompts-member',
    route: `${ROUTE}?prompt=codeReviewer`,
    shell: true,
    arrange: (p) => arrange(p, false),
    ready,
  },
  {
    name: 'prompts-revert',
    route: `${ROUTE}?prompt=brief`,
    shell: true,
    arrange: (p) => arrange(p),
    ready,
    act: async (p) => {
      await p.getByTestId(ids.actionUseBuiltIn).click();
      await expect(p.getByTestId(ids.useBuiltInDialog)).toBeVisible();
    },
  },
  {
    name: 'prompts-edit',
    route: `${ROUTE}?prompt=lead`,
    shell: true,
    arrange: (p) => arrange(p),
    ready,
    act: async (p) => {
      await p.getByTestId(ids.actionCustomize).click();
      await p.getByTestId(ids.editorText).fill(fx.edits.lead);
      await expect(p.getByTestId(ids.editorText)).toHaveValue(fx.edits.lead);
    },
  },
  {
    name: 'prompts-tags',
    route: `${ROUTE}?prompt=brief`,
    shell: true,
    arrange: (p) => arrange(p),
    ready,
    act: async (p) => {
      await p.getByTestId(ids.actionEdit).click();
      await p.getByTestId(ids.editorText).fill(fx.edits.brief);
      await expect(p.getByTestId(ids.editorText)).toHaveValue(fx.edits.brief);
    },
  },
  {
    name: 'prompts-search',
    route: `${ROUTE}?prompt=brief`,
    shell: true,
    arrange: (p) => arrange(p),
    ready,
    act: async (p) => {
      await p.getByTestId(ids.searchInput).fill('task_key');
      await expect(p.getByTestId(ids.searchResults)).toBeVisible();
    },
  },
  {
    name: 'prompts-single',
    route: `${ROUTE}?prompt=work`,
    shell: true,
    arrange: (p) => arrange(p),
    ready: async (p) => {
      await ready(p);
      await expect(p.getByTestId(ids.stageWhen)).toBeVisible();
    },
  },
]);
