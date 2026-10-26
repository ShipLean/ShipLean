import { expect, test } from '@playwright/experimental-ct-react';
import type { CriteriaClause, CriteriaJson } from '../api/persisted';
import { componentTestIds } from '../components/testIds';
import { criteriaTestIds as ids } from '../features/criteria/testIds';
import { CriteriaBuilderHarness } from './ct/CriteriaBuilderHarness';
import { criteriaHarnessTestIds as harness } from './ct/harnessIds';
import { CriteriaChips } from './CriteriaChips';

const priority = { field: 'priority', op: '=', value: 1 } as const;
const tag = { field: 'tags', op: 'contains', value: 'Blocked' } as const;
const joel = {
  id: '0199f000-0000-7000-8000-000000000001',
  name: 'Joel',
  initials: 'JD',
  avatarColour: '#2f7fd8',
  photoUrl: null,
};

const two: CriteriaJson = { match: 'all', clauses: [priority, tag] };

test('CriteriaBuilder_WhenClauseInvalid_ShouldShowError', async ({ mount, page }) => {
  await mount(
    <CriteriaBuilderHarness
      initial={{ match: 'all', clauses: [priority, { field: 'priority', op: '=', value: null }] }}
      errors={[
        { clauseIndex: 0, field: 'priority', code: 'invalidValue', message: 'The server says no.' },
      ]}
    />,
  );

  const clauses = page.getByTestId(ids.clause);
  await expect(clauses.nth(0).getByTestId(ids.error)).toHaveText('The server says no.');
  await expect(clauses.nth(1).getByTestId(ids.error)).toHaveText(
    'The value is not valid for Priority.',
  );
  await clauses.nth(1).getByTestId(ids.value).selectOption('2');
  await expect(clauses.nth(1).getByTestId(ids.error)).toHaveCount(0);
});

test('CriteriaBuilder_WhenAnyChosen_ShouldEmitMatchAny', async ({ mount, page }) => {
  await mount(<CriteriaBuilderHarness initial={two} />);

  await page.getByTestId(ids.matchAny).click();

  await expect(page.getByTestId(harness.ctEmitted)).toHaveText(
    JSON.stringify({ match: 'any', clauses: [priority, tag] }),
  );
  await expect(page.getByTestId(ids.matchAny)).toHaveAttribute('aria-pressed', 'true');
});

test('CriteriaBuilder_WhenClauseRemoved_ShouldEmitTheRest', async ({ mount, page }) => {
  await mount(<CriteriaBuilderHarness initial={two} />);

  await page.getByTestId(ids.remove).first().click();

  await expect(page.getByTestId(harness.ctEmitted)).toHaveText(
    JSON.stringify({ match: 'all', clauses: [tag] }),
  );
  await expect(page.getByTestId(ids.clause)).toHaveCount(1);
});

test('CriteriaBuilder_WhenClauseAdded_ShouldEmitAValidTypeClause', async ({ mount, page }) => {
  await mount(<CriteriaBuilderHarness initial={{ match: 'all', clauses: [priority] }} />);

  await page.getByTestId(ids.add).click();

  await expect(page.getByTestId(harness.ctEmitted)).toHaveText(
    JSON.stringify({
      match: 'all',
      clauses: [priority, { field: 'type', op: '=', value: 'epic' }],
    }),
  );
  await expect(page.getByTestId(ids.error)).toHaveCount(0);
});

test('CriteriaBuilder_WhenFieldChanged_ShouldResetOperatorAndValue', async ({ mount, page }) => {
  await mount(<CriteriaBuilderHarness initial={{ match: 'all', clauses: [priority] }} />);

  await page.getByTestId(ids.field).selectOption('tags');

  await expect(page.getByTestId(harness.ctEmitted)).toHaveText(
    JSON.stringify({ match: 'all', clauses: [{ field: 'tags', op: 'contains', value: '' }] }),
  );
});

test('CriteriaBuilder_WhenIsEmptyChosen_ShouldEmitNullValueAndHideTheInput', async ({
  mount,
  page,
}) => {
  await mount(
    <CriteriaBuilderHarness
      initial={{ match: 'all', clauses: [priority] }}
      fields={['points', 'priority']}
    />,
  );

  await page.getByTestId(ids.field).selectOption('points');
  await page.getByTestId(ids.op).selectOption('isEmpty');

  await expect(page.getByTestId(harness.ctEmitted)).toHaveText(
    JSON.stringify({ match: 'all', clauses: [{ field: 'points', op: 'isEmpty', value: null }] }),
  );
  await expect(page.getByTestId(ids.value)).toHaveCount(0);
});

test('CriteriaBuilder_WhenUsersAndInChosen_ShouldEmitTheTickedValues', async ({ mount, page }) => {
  await mount(
    <CriteriaBuilderHarness
      initial={{ match: 'all', clauses: [{ field: 'assignee', op: 'in', value: ['@me'] }] }}
      users={[joel]}
    />,
  );

  await page.getByTestId(ids.inOption).nth(1).check();
  await page.getByTestId(ids.inNone).check();

  await expect(page.getByTestId(harness.ctEmitted)).toHaveText(
    JSON.stringify({
      match: 'all',
      clauses: [{ field: 'assignee', op: 'in', value: ['@me', joel.id, null] }],
    }),
  );
});

test('CriteriaChips_ShouldSummariseEachClause', async ({ mount, page }) => {
  await mount(
    <CriteriaChips
      value={{
        match: 'any',
        clauses: [
          priority,
          tag,
          { field: 'assignee', op: 'in', value: ['@me', joel.id, null] },
          { field: 'points', op: 'isEmpty', value: null },
        ],
      }}
      users={[joel]}
    />,
  );

  const chips = page.getByTestId(ids.chips).getByTestId(componentTestIds.chip);
  await expect(chips).toHaveText([
    'Priority = 1',
    'or Tags contains Blocked',
    'or Assigned to in Me, Joel, none',
    'or Story points is empty',
  ]);
});

test('CriteriaChips_WhenChipRemoved_ShouldReportItsIndex', async ({ mount, page }) => {
  const removed: number[] = [];
  await mount(<CriteriaChips value={two} onRemove={(index) => removed.push(index)} />);

  await page.getByTestId(componentTestIds.chipRemove).nth(1).click();

  expect(removed).toEqual([1]);
});

const ID_A = '00000005-0000-4000-8001-000000000101';
const ID_B = '00000005-0000-4000-8001-000000000102';
const ID_C = '00000005-0000-4000-8001-000000000103';

test('CriteriaBuilder_WhenFirstClauseRemoved_ShouldKeepTheSecondsValue', async ({
  mount,
  page,
}) => {
  const second: CriteriaClause = { field: 'parent', op: 'in', value: [ID_B] };
  await mount(
    <CriteriaBuilderHarness
      initial={{ match: 'all', clauses: [{ field: 'id', op: 'in', value: [ID_A] }, second] }}
    />,
  );

  await page.getByTestId(ids.remove).first().click();
  const remaining = page.getByTestId(ids.value);
  await expect(remaining).toHaveValue(ID_B);
  await remaining.focus();
  await remaining.blur();

  await expect(page.getByTestId(harness.ctEmitted)).toHaveText(
    JSON.stringify({ match: 'all', clauses: [second] }),
  );
});

test('CriteriaBuilder_WhenValueReplacedByTheParent_ShouldShowTheNewList', async ({
  mount,
  page,
}) => {
  await mount(
    <CriteriaBuilderHarness
      initial={{ match: 'all', clauses: [{ field: 'id', op: 'in', value: [ID_A] }] }}
      replaceWith={{ match: 'all', clauses: [{ field: 'id', op: 'in', value: [ID_C] }] }}
    />,
  );
  await page.getByTestId(ids.value).fill(`${ID_A}, ${ID_B}`);

  await page.getByTestId(harness.ctReplace).click();

  await expect(page.getByTestId(ids.value)).toHaveValue(ID_C);
});

test('CriteriaBuilder_WhenListTypedWithTrailingComma_ShouldCommitOnBlur', async ({
  mount,
  page,
}) => {
  await mount(
    <CriteriaBuilderHarness
      initial={{ match: 'all', clauses: [{ field: 'id', op: 'in', value: [ID_A] }] }}
    />,
  );

  const input = page.getByTestId(ids.value);
  await input.fill(`${ID_A}, ${ID_B},`);
  await expect(page.getByTestId(harness.ctEmitted)).toHaveText(/"value":\["0+5-[^"]*101"\]/);
  await input.blur();

  await expect(page.getByTestId(harness.ctEmitted)).toHaveText(
    JSON.stringify({ match: 'all', clauses: [{ field: 'id', op: 'in', value: [ID_A, ID_B] }] }),
  );
});

test('CriteriaBuilder_WhenPointsInTyped_ShouldEmitNumbers', async ({ mount, page }) => {
  await mount(
    <CriteriaBuilderHarness
      initial={{ match: 'all', clauses: [{ field: 'points', op: 'in', value: [1] }] }}
    />,
  );

  await page.getByTestId(ids.value).fill('3, 5, 0.5');
  await page.getByTestId(ids.value).blur();

  await expect(page.getByTestId(harness.ctEmitted)).toHaveText(
    JSON.stringify({ match: 'all', clauses: [{ field: 'points', op: 'in', value: [3, 5, 0.5] }] }),
  );
  await expect(page.getByTestId(ids.error)).toHaveCount(0);
});

test('CriteriaBuilder_WhenChipsHidden_ShouldNotRenderThem', async ({ mount, page }) => {
  await mount(<CriteriaBuilderHarness initial={two} showChips={false} />);

  await expect(page.getByTestId(ids.clause)).toHaveCount(2);
  await expect(page.getByTestId(ids.chips)).toHaveCount(0);
});

test('CriteriaBuilder_ShouldShowChipsByDefault', async ({ mount, page }) => {
  await mount(<CriteriaBuilderHarness initial={two} />);

  await expect(page.getByTestId(ids.chips)).toHaveCount(1);
});

test('CriteriaBuilder_WhenClauseInvalid_ShouldLinkTheControlToItsError', async ({
  mount,
  page,
}) => {
  await mount(
    <CriteriaBuilderHarness
      initial={{ match: 'all', clauses: [priority, { field: 'priority', op: '=', value: null }] }}
    />,
  );

  const clauses = page.getByTestId(ids.clause);
  const control = clauses.nth(1).getByTestId(ids.value);
  const errorId = await clauses.nth(1).getByTestId(ids.error).getAttribute('id');
  await expect(control).toHaveAttribute('aria-invalid', 'true');
  await expect(control).toHaveAttribute('aria-describedby', errorId ?? 'missing');
  await expect(clauses.nth(0).getByTestId(ids.value)).toHaveAttribute('aria-invalid', 'false');
});

test('CriteriaBuilder_ShouldNameEachClauseOnItsRemoveButton', async ({ mount, page }) => {
  await mount(<CriteriaBuilderHarness initial={two} />);

  await expect(page.getByTestId(ids.remove).nth(0)).toHaveAttribute(
    'aria-label',
    'Remove clause 1: Priority = 1',
  );
  await expect(page.getByTestId(ids.remove).nth(1)).toHaveAttribute(
    'aria-label',
    'Remove clause 2: Tags contains Blocked',
  );
});

test('CriteriaBuilder_WhenFirstClauseRemoved_ShouldKeepTheSecondRowsElement', async ({
  mount,
  page,
}) => {
  await mount(<CriteriaBuilderHarness initial={two} />);
  await page
    .getByTestId(ids.clause)
    .nth(1)
    .evaluate((row) => row.setAttribute('data-marker', 'kept'));

  await page.getByTestId(ids.remove).first().click();

  await expect(page.getByTestId(ids.clause)).toHaveCount(1);
  await expect(page.getByTestId(ids.clause)).toHaveAttribute('data-marker', 'kept');
});
