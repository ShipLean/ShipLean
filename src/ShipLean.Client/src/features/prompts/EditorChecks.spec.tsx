import { expect, test } from '@playwright/experimental-ct-react';
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
  expect(changes[0]).toContain('<claude-code>\nx\n</claudecode>');
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
