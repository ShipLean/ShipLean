import { expect, test } from '@playwright/experimental-ct-react';
import type { PromptAssembledDto, PromptHistoryDto } from '@shiplean/core/api/prompts';
import assembledJson from '../../../../ShipLean.TestSupport/DtoFixtures/prompts/prompt-assembled.json' with { type: 'json' };
import historyJson from '../../../../ShipLean.TestSupport/DtoFixtures/prompts/prompt-history.json' with { type: 'json' };
import { PromptsHarness } from './ct/PromptsHarness';
import {
  PROMPTS_OPENED_URL,
  PROMPTS_URL,
  adminPage,
  adminProject,
  memberPage,
  memberProject,
} from './ct/promptsData';
import { promptsTestIds as ids } from './testIds';

const history = historyJson as PromptHistoryDto;
const assembled = assembledJson as PromptAssembledDto;
const HISTORY_URL = '**/api/projects/*/prompts/brief/history';
const ASSEMBLED_URL = '**/api/projects/*/prompts/brief/assembled?*';
const USE_URL = '**/api/projects/*/prompts/brief/versions/1/use';

async function open(
  mount: Parameters<Parameters<typeof test>[2]>[0]['mount'],
  page: Parameters<Parameters<typeof test>[2]>[0]['page'],
  prompt: string,
  admin = true,
  pageJson = admin ? adminPage : memberPage,
) {
  await page.route(PROMPTS_URL, (route) => route.fulfill({ json: pageJson }));
  await page.route(PROMPTS_OPENED_URL, (route) => route.fulfill({ status: 204 }));
  await mount(<PromptsHarness project={admin ? adminProject : memberProject} />, {
    hooksConfig: { route: `/p/SHIP/settings/prompts?prompt=${prompt}` },
  });
}

test('SourceTab_WhenSectionErrors_ShouldWarnThatOnlyUntaggedTextIsServed', async ({
  mount,
  page,
}) => {
  await open(mount, page, 'guide');

  await expect(page.getByTestId(ids.sectionErrorFlag)).toContainText(
    'every agent gets only the untagged text',
  );
});

test('SourceTab_WhenCustom_ShouldHighlightPlaceholdersAndAgentTags', async ({ mount, page }) => {
  const custom = adminPage.stages.flatMap((s) => s.prompts).find((p) => p.key === 'brief')?.custom;
  const text =
    '# Brief\nFor {{key}} here\n<claude-code>\nonly this\n</claude-code>\nmid <cursor> tag\n';
  const edited = {
    ...adminPage,
    stages: adminPage.stages.map((s) => ({
      ...s,
      prompts: s.prompts.map((p) =>
        p.key === 'brief' && custom ? { ...p, custom: { ...custom, text } } : p,
      ),
    })),
  };
  await open(mount, page, 'brief', true, edited);

  await expect(page.getByTestId(ids.sourceHeading)).toHaveText('# Brief');
  await expect(page.getByTestId(ids.sourcePlaceholder)).toHaveText('{{key}}');
  await expect(page.getByTestId(ids.sourceAgentTag)).toHaveCount(2);
  await expect(page.getByTestId(ids.sectionErrorFlag)).toHaveCount(0);
});

test('RenderedTab_ShouldShowTheServerRenderedHtml', async ({ mount, page }) => {
  await page.route('**/api/markdown/render', (route) =>
    route.fulfill({
      json: { markdown: '', html: '<h2>Server rendered</h2>', images: [] },
    }),
  );
  await open(mount, page, 'brief');
  await page.getByTestId(ids.tabRendered).click();

  await expect(page.getByTestId(ids.renderedBody).getByRole('heading')).toHaveText(
    'Server rendered',
  );
});

test('YourChangesTab_WhenPatch_ShouldDrawTheDiff', async ({ mount, page }) => {
  await open(mount, page, 'brief');
  await page.getByTestId(ids.tabYourChanges).click();

  await expect(page.getByTestId(ids.yourChangesDiff)).toBeVisible();
  await expect(page.getByTestId(ids.diffLine).and(page.locator('[data-kind=add]'))).toHaveText(
    /line six, reworded/,
  );
  await expect(page.getByTestId(ids.diffLine).and(page.locator('[data-kind=del]'))).toHaveCount(1);
});

test('YourChangesTab_WhenNoPatch_ShouldSayNoChanges', async ({ mount, page }) => {
  const noPatch = {
    ...adminPage,
    stages: adminPage.stages.map((s) => ({
      ...s,
      prompts: s.prompts.map((p) => (p.key === 'brief' ? { ...p, yourChangesPatch: null } : p)),
    })),
  };
  await open(mount, page, 'brief', true, noPatch);
  await page.getByTestId(ids.tabYourChanges).click();

  await expect(page.getByTestId(ids.yourChangesEmpty)).toHaveText('No changes');
});

test('HistoryTab_ShouldListEveryVersionNewestFirstAndMarkTheOneInUse', async ({ mount, page }) => {
  await page.route(HISTORY_URL, (route) => route.fulfill({ json: history }));
  await open(mount, page, 'brief');
  await page.getByTestId(ids.tabHistory).click();

  await expect(page.getByTestId(ids.historyRowInUse)).toContainText('v2');
  await expect(page.getByTestId(ids.historyRowInUse)).toContainText('In use');
  await expect(page.getByTestId(ids.historyRow)).toHaveCount(1);
  await expect(page.getByTestId(ids.historyRow)).toContainText('v1');
  await expect(page.getByTestId(ids.historyBuiltInRow)).toContainText('built-in of 10 Nov');
  await expect(page.getByTestId(ids.historyBuiltInUse)).toBeVisible();
});

test('HistoryTab_WhenUseClicked_ShouldPutThatVersionBack', async ({ mount, page }) => {
  await page.route(HISTORY_URL, (route) => route.fulfill({ json: history }));
  const used = page.waitForRequest(
    (r) => r.method() === 'POST' && r.url().endsWith('/versions/1/use'),
  );
  await page.route(USE_URL, (route) =>
    route.fulfill({ json: { key: 'brief', status: 'custom', inUseVersion: 1 } }),
  );
  await open(mount, page, 'brief');
  await page.getByTestId(ids.tabHistory).click();

  await page.getByTestId(ids.historyUse).click();
  await expect(page.getByTestId(ids.historyNote)).toContainText(
    'Agents get v1 from their next call',
  );
  await page.getByRole('button', { name: 'Confirm' }).click();

  await used;
});

test('HistoryTab_WhenMember_ShouldOfferNoUse', async ({ mount, page }) => {
  await page.route(HISTORY_URL, (route) => route.fulfill({ json: history }));
  await open(mount, page, 'brief', false);
  await page.getByTestId(ids.tabHistory).click();

  await expect(page.getByTestId(ids.historyRow)).toHaveCount(1);
  await expect(page.getByTestId(ids.historyUse)).toHaveCount(0);
  await expect(page.getByTestId(ids.historyBuiltInUse)).toHaveCount(0);
});

test('AssembledTab_ShouldShowTheLayersInOrderWithPlannedOnesMarked', async ({ mount, page }) => {
  await page.route(ASSEMBLED_URL, (route) => route.fulfill({ json: assembled }));
  await open(mount, page, 'brief');
  await page.getByTestId(ids.tabAssembled).click();

  await expect(page.getByTestId(ids.assembledLayers).getByRole('listitem')).toHaveCount(5);
  await expect(page.getByTestId(ids.assembledLayerThis)).toContainText('Task brief, custom v2');
  await expect(page.getByTestId(ids.assembledLayerPlanned)).toHaveCount(2);
  await expect(page.getByTestId(ids.assembledLayerPlanned).first()).toContainText(
    'Ways of working',
  );
  await expect(page.getByTestId(ids.assembledText)).toContainText('Second line, reworded');
});

test('AssembledTab_WhenAgentChanged_ShouldShowThatAgentsText', async ({ mount, page }) => {
  await page.route(ASSEMBLED_URL, (route) => {
    const agent = new URL(route.request().url()).searchParams.get('agent');
    return route.fulfill({
      json: { ...assembled, agent, text: `Text for ${agent}\n` },
    });
  });
  await open(mount, page, 'brief');
  await page.getByTestId(ids.tabAssembled).click();
  await expect(page.getByTestId(ids.assembledText)).toContainText('Text for claude-code');

  await page.getByTestId(ids.agentOption).filter({ hasText: 'Cursor' }).click();

  await expect(page.getByTestId(ids.assembledText)).toContainText('Text for cursor');
});
