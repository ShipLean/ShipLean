import { expect } from '@playwright/experimental-ct-react';
import { promptsTestIds as ids } from '../../features/prompts/testIds';
import { projectSettings } from './fixtures/workItemSettings';
import fx from './fixtures/waysOfWorking.fixture.json' with { type: 'json' };
import { phonePageShots } from './shootPhone';
import { stubShell } from './shellStub';
import type { Page } from './shoot';

// Ways of working on a phone (390×844): the stage navigation stacks above the prompt, which runs full width.
// Driven by the same fixture as waysOfWorking.spec.tsx. Runs only in the CT phone project.
const ROUTE = '/p/SHIP/ways-of-working';

async function arrange(page: Page) {
  await stubShell(page);
  await page.route('**/api/projects/p1/settings', (r) => r.fulfill({ json: projectSettings }));
  await page.route('**/api/projects/*/prompts', (r) =>
    r.fulfill({ json: { ...fx.page, canEdit: true } }),
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

async function ready(page: Page) {
  await expect(page.getByTestId(ids.header)).toBeVisible();
  await expect(page.getByTestId(ids.stageNav)).toBeVisible();
  await expect(page.getByTestId(ids.detailName)).toBeVisible();
}

// fill() leaves the editor focused and scrolled to its end; a shot shows the editor's top at rest, as the mock-up does.
async function atRest(page: Page) {
  await page.getByTestId(ids.editorText).evaluate((el: HTMLTextAreaElement) => {
    el.blur();
    el.scrollTop = 0;
  });
  await page.evaluate(() => window.scrollTo(0, 0));
}

phonePageShots([
  {
    name: 'prompts-phone',
    route: `${ROUTE}?prompt=brief`,
    shell: true,
    arrange,
    ready,
  },
  {
    name: 'prompts-phone-edit',
    route: `${ROUTE}?prompt=lead`,
    shell: true,
    arrange,
    ready,
    act: async (p) => {
      await p.getByTestId(ids.actionCustomize).click();
      await p.getByTestId(ids.editorText).fill(fx.edits.lead);
      await expect(p.getByTestId(ids.editorText)).toHaveValue(fx.edits.lead);
      await atRest(p);
    },
  },
]);
