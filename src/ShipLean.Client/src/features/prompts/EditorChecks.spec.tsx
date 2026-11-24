import { expect, test } from '@playwright/experimental-ct-react';
import type { Page } from 'playwright';
import type { PromptDto } from '@shiplean/core/api/prompts';
import { adminPage } from './ct/promptsData';
import { EditorChecks } from './editor/EditorChecks';
import { promptsTestIds as ids } from './testIds';

const brief = adminPage.stages
  .flatMap((stage) => stage.prompts)
  .find((prompt) => prompt.key === 'brief') as PromptDto;
const TOKEN = brief.placeholders[1]!;
const full = `${brief.placeholders.join('\n')}\n`;

test('EditorChecks_WhenPlaceholderRemoved_ShouldWarnAndPutItBack', async ({ mount }) => {
  const changes: string[] = [];
  const text = full.replace(`${TOKEN}\n`, '');
  const component = await mount(
    <EditorChecks text={text} prompt={brief} onChange={(next) => changes.push(next)} />,
  );
  await expect(component.getByTestId(ids.checkPlaceholderMissing)).toHaveText(TOKEN);
  await component.getByTestId(ids.checkPutBack).click();
  expect(changes).toHaveLength(1);
  expect(changes[0]).toContain(TOKEN);
});

test('EditorChecks_WhenTagUnclosed_ShouldReportAnError', async ({ mount }) => {
  const component = await mount(
    <EditorChecks text={`${full}<cursor>\nonly`} prompt={brief} onChange={() => undefined} />,
  );
  await expect(component.getByTestId(ids.checkUnclosedTag)).toContainText('Line 7');
  await expect(component.getByTestId(ids.checkProblem)).toContainText('Save is blocked');
});

test('EditorChecks_WhenTagMisspelt_ShouldOfferTheFix', async ({ mount }) => {
  const changes: string[] = [];
  const text = `${full}<claudecode>\nx\n</claudecode>`;
  const component = await mount(
    <EditorChecks text={text} prompt={brief} onChange={(next) => changes.push(next)} />,
  );
  await expect(component.getByTestId(ids.checkUnknownTag).first()).toContainText('<claude-code>');
  await component.getByTestId(ids.checkFixTag).first().click();
  // One warning per tag, so Fix it fixes the open and the close tag together (design-gaps §13).
  expect(changes[0]).toContain('<claude-code>\nx\n</claude-code>');
});

test('EditorChecks_WhenAddSectionClicked_ShouldAppendTheTags', async ({ mount }) => {
  const changes: string[] = [];
  const component = await mount(
    <EditorChecks text={full} prompt={brief} onChange={(next) => changes.push(next)} />,
  );
  await component.getByTestId(ids.checkAddCursor).click();
  expect(changes).toEqual([`${full}\n<cursor>\n\n</cursor>\n`]);
});

test('EditorChecks_ShouldCountSectionsPerAgent', async ({ mount }) => {
  const text = `${full}<cursor>\na\n</cursor>\n<cursor>\nb\n</cursor>\n<claude-code>\nc\n</claude-code>`;
  const component = await mount(
    <EditorChecks text={text} prompt={brief} onChange={() => undefined} />,
  );
  const count = (agent: string) =>
    component.locator(`[data-testid="${ids.checkSectionCount}"][data-agent="${agent}"]`);
  await expect(count('cursor')).toHaveText('2 sections');
  await expect(count('claude-code')).toHaveText('1 section');
  await expect(count('codex-cli')).toHaveText('none');
});

// The computed colour of a design token, to compare a rendered colour with.
function tokenColor(page: Page, token: string): Promise<string> {
  return page.evaluate((name) => {
    const probe = document.createElement('span');
    probe.style.color = `var(${name})`;
    document.body.append(probe);
    const color = getComputedStyle(probe).color;
    probe.remove();
    return color;
  }, token);
}

test('EditorChecks_WhenPlaceholderMissing_ShouldStrikeItAmberWithPutBack', async ({
  mount,
  page,
}) => {
  const text = full.replace(`${TOKEN}\n`, '');
  const component = await mount(
    <EditorChecks text={text} prompt={brief} onChange={() => undefined} />,
  );
  const chip = component.getByTestId(ids.checkPlaceholderMissing);
  const warn = await tokenColor(page, '--color-warn');

  await expect(chip).toHaveText(TOKEN);
  await expect(chip).toHaveCSS('text-decoration-line', 'line-through');
  const tint = await chip.evaluate((el) => getComputedStyle(el).backgroundColor);
  expect(tint).not.toBe('rgba(0, 0, 0, 0)');
  await expect(component.getByTestId(ids.checkPutBack)).toBeVisible();
  await expect(component.getByTestId(ids.checkPlaceholderOk)).toHaveCount(
    brief.placeholders.length - 1,
  );
  await expect(component.getByTestId(ids.checkPlaceholderWarning)).toHaveText(
    `Without ${TOKEN} agents won’t get that part. You can still save.`,
  );
  await expect(component.getByTestId(ids.checkPlaceholderWarning)).toHaveCSS('color', warn);
});

test('EditorChecks_WhenUnknownTagOpenedAndClosed_ShouldWarnOnce', async ({ mount }) => {
  const changes: string[] = [];
  const text = `${full}<claudecode>\nx\n</claudecode>\n`;
  const component = await mount(
    <EditorChecks text={text} prompt={brief} onChange={(next) => changes.push(next)} />,
  );
  const warning = component.getByTestId(ids.checkUnknownTag);

  await expect(warning).toHaveCount(1);
  await expect(warning).toHaveText(
    '<claudecode> isn’t an agent ShipLean recognises, so every agent gets it as typed. Did you mean <claude-code>? Fix it',
  );
  await component.getByTestId(ids.checkFixTag).click();
  expect(changes).toEqual([`${full}<claude-code>\nx\n</claude-code>\n`]);
});

test('EditorChecks_WarningsShouldBeAmberAndErrorsRed', async ({ mount, page }) => {
  const text = `${full.replace(`${TOKEN}\n`, '')}{{not_ours}}\n<claudecode>\nx\n</claudecode>\n<cursor>\nopen`;
  const component = await mount(
    <EditorChecks text={text} prompt={brief} onChange={() => undefined} />,
  );
  const warn = await tokenColor(page, '--color-warn');
  const danger = await tokenColor(page, '--color-danger');
  expect(warn).not.toBe(danger);

  await expect(component.getByTestId(ids.checkPlaceholderWarning)).toHaveCSS('color', warn);
  await expect(component.getByTestId(ids.checkPlaceholderUnknown)).toHaveCSS('color', warn);
  await expect(component.getByTestId(ids.checkUnknownTag)).toHaveCSS('color', warn);
  await expect(component.getByTestId(ids.checkUnclosedTag)).toHaveCSS('color', danger);
  await expect(component.getByTestId(ids.checkProblem)).toHaveCSS('color', danger);
});
