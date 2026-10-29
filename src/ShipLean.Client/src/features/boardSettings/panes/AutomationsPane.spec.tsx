import { expect, test } from '@playwright/experimental-ct-react';
import type { BoardLevel, BoardSettingsDto, BoardSettingsPut } from '../../../api/boards';
import type { TransitionJson } from '../../../api/persisted';
import { settingsDialogTestIds as dialogIds } from '../../settingsDialog/testIds';
import { BoardSettingsDialog } from '../BoardSettingsDialog';
import { BOARD_ID, boardSettingsDto, emptySettings, PROJECT_ID } from '../ct/boardSettingsFixtures';
import { boardSettingsTestIds as ids } from '../testIds';
import { AutomationsPane } from './AutomationsPane';

const story = {
  new: { id: 's-new', name: 'New', category: 'proposed' },
  idea: { id: 's-idea', name: 'Idea', category: 'proposed' },
  active: { id: 's-active', name: 'Active', category: 'inProgress' },
  dropped: { id: 's-dropped', name: 'Dropped', category: 'removed' },
} as const;
const bug = {
  new: { id: 'b-new', name: 'New', category: 'proposed' },
  active: { id: 'b-active', name: 'Active', category: 'inProgress' },
} as const;

const toRef = (s: { id: string; name: string }) => ({ stateId: s.id, name: s.name });
const transition = (
  id: string,
  from: { id: string; name: string } | null,
  to: { id: string; name: string },
  prompt = '',
): TransitionJson => ({
  id,
  type: 'story',
  from: from === null ? null : toRef(from),
  to: toRef(to),
  actions: [{ id: `${id}-a`, kind: 'agent', prompt, connectionId: null }],
});

function board(level: BoardLevel, transitions: TransitionJson[] = []): BoardSettingsDto {
  return boardSettingsDto({
    level,
    states: [
      { type: 'story', states: Object.values(story) },
      { type: 'bug', states: Object.values(bug) },
      { type: 'feature', states: [story.new, story.idea, story.active] },
    ],
    settings: { ...emptySettings(), transitions },
  });
}

interface StubbedRoute {
  request(): { method(): string; postDataJSON(): unknown };
  fulfill(response: { json: unknown; status?: number }): Promise<void>;
}
interface Routable {
  route(url: string, handler: (route: StubbedRoute) => Promise<void>): Promise<unknown>;
}

const CONN_A = '00000000-0000-4000-8000-0000000000c1';
const CONN_B = '00000000-0000-4000-8000-0000000000c2';

async function stubProject(page: Routable) {
  await page.route(`**/api/projects/${PROJECT_ID}/settings`, (route) =>
    route.fulfill({
      json: {
        general: {
          agentConnectionId: CONN_A,
          agentConnections: [
            { id: CONN_A, name: 'Home agent', archived: false },
            { id: CONN_B, name: 'Cloud agent', archived: false },
          ],
        },
      },
    }),
  );
}

async function stubBoard(page: Routable, dto: BoardSettingsDto): Promise<BoardSettingsPut[]> {
  const puts: BoardSettingsPut[] = [];
  await stubProject(page);
  await page.route(`**/api/projects/${PROJECT_ID}/boards`, (route) =>
    route.fulfill({ json: [{ id: BOARD_ID, level: dto.level, name: dto.level }] }),
  );
  await page.route(`**/api/boards/${BOARD_ID}/settings`, async (route) => {
    if (route.request().method() === 'GET') return route.fulfill({ json: dto });
    const body = route.request().postDataJSON() as BoardSettingsPut;
    puts.push(body);
    return route.fulfill({
      json: { board: { ...dto, settings: body.settings }, mergedSections: [] },
    });
  });
  return puts;
}

const mountDialog = (
  mount: (component: React.JSX.Element) => Promise<unknown>,
  level: BoardLevel,
  section = 'automations',
) =>
  mount(
    <BoardSettingsDialog
      projectId={PROJECT_ID}
      level={level}
      section={section}
      onSectionChange={() => undefined}
      onClose={() => undefined}
    />,
  );

test('AutomationsPane_WhenEpicsBoard_ShouldHaveNoTabAndFallBackToColumns', async ({
  mount,
  page,
}) => {
  await stubBoard(page, board('epics'));
  await mountDialog(mount, 'epics');
  await expect(page.getByTestId(`${dialogIds.navItem}-columns`)).toBeVisible();
  await expect(page.getByTestId(`${dialogIds.navItem}-automations`)).toHaveCount(0);
  await expect(page.getByTestId(ids.columnsPane)).toBeAttached();
  await expect(page.getByTestId(ids.automationsPane)).toHaveCount(0);
});

test('AutomationsPane_WhenStoriesBoard_ShouldHaveTheTab', async ({ mount, page }) => {
  await stubBoard(page, board('stories'));
  await mountDialog(mount, 'stories');
  await expect(page.getByTestId(`${dialogIds.navItem}-automations`)).toBeVisible();
  await expect(page.getByTestId(ids.automationsEmpty)).toBeVisible();
});

test('AutomationsPane_WhenAddedThenDeleted_ShouldListAndRemoveTheRow', async ({ mount, page }) => {
  await stubBoard(page, board('stories'));
  await mountDialog(mount, 'stories');

  await page.getByTestId(ids.transitionAddFrom).selectOption({ label: 'New' });
  await page.getByTestId(ids.transitionAddTo).selectOption({ label: 'Idea' });
  await page.getByTestId(ids.transitionAdd).click();
  await expect(page.getByTestId(ids.transitionRow)).toHaveCount(1);
  await expect(page.getByTestId(ids.transitionLabel)).toHaveText('Story · New → Idea');
  await expect(page.getByTestId(ids.transitionSummary)).toHaveText('Send to agent: no prompt yet');
  await expect(page.getByTestId(ids.transitionInvalid)).toHaveCount(0);

  await page.getByTestId(ids.transitionDelete).click();
  await expect(page.getByTestId(ids.transitionRow)).toHaveCount(0);
  await expect(page.getByTestId(ids.automationsEmpty)).toBeVisible();
});

test('AutomationsPane_WhenBugChosen_ShouldOfferTheBugStatesAndAnyState', async ({
  mount,
  page,
}) => {
  await stubBoard(page, board('stories'));
  await mountDialog(mount, 'stories');

  await page.getByTestId(ids.transitionAddType).selectOption({ label: 'Bug' });
  await expect(page.getByTestId(ids.transitionAddFrom).locator('option')).toHaveText([
    'Any state',
    'New',
    'Active',
  ]);
  await page.getByTestId(ids.transitionAddTo).selectOption({ label: 'Active' });
  await page.getByTestId(ids.transitionAdd).click();
  await expect(page.getByTestId(ids.transitionLabel)).toHaveText('Bug · Any state → Active');
});

test('AutomationsPane_WhenFeaturesBoard_ShouldHaveNoTypePicker', async ({ mount, page }) => {
  await stubBoard(page, board('features'));
  await mountDialog(mount, 'features');
  await expect(page.getByTestId(ids.transitionAddType)).toHaveCount(0);
  await page.getByTestId(ids.transitionAddTo).selectOption({ label: 'Idea' });
  await page.getByTestId(ids.transitionAdd).click();
  await expect(page.getByTestId(ids.transitionLabel)).toHaveText('Feature · Any state → Idea');
});

test('AutomationsPane_WhenDuplicate_ShouldRefuseItInline', async ({ mount, page }) => {
  await stubBoard(page, board('stories', [transition('t1', story.new, story.idea)]));
  await mountDialog(mount, 'stories');

  await page.getByTestId(ids.transitionAddFrom).selectOption({ label: 'New' });
  await page.getByTestId(ids.transitionAddTo).selectOption({ label: 'Idea' });
  await page.getByTestId(ids.transitionAdd).click();
  await expect(page.getByTestId(ids.transitionAddRefusal)).toHaveText(
    'That transition is already defined.',
  );
  await expect(page.getByTestId(ids.transitionRow)).toHaveCount(1);

  // the same states from Any state is a different transition, and From = To is refused
  await page.getByTestId(ids.transitionAddFrom).selectOption({ label: 'Idea' });
  await page.getByTestId(ids.transitionAdd).click();
  await expect(page.getByTestId(ids.transitionAddRefusal)).toHaveText(
    'From and To must be different states.',
  );
  await page.getByTestId(ids.transitionAddFrom).selectOption({ label: 'Any state' });
  await page.getByTestId(ids.transitionAdd).click();
  await expect(page.getByTestId(ids.transitionRow)).toHaveCount(2);
});

test('AutomationsPane_WhenStateWasRemoved_ShouldMarkTheRowInvalidWithTheReason', async ({
  mount,
  page,
}) => {
  await stubBoard(
    page,
    board('stories', [
      transition('t1', story.new, story.idea),
      transition('t2', null, { id: 's-deleted', name: 'Review' }),
      transition('t3', { id: 's-gone', name: 'Gone' }, story.active),
      transition('t4', story.dropped, story.active),
    ]),
  );
  await mountDialog(mount, 'stories');

  const rows = page.getByTestId(ids.transitionRow);
  await expect(rows.nth(0).getByTestId(ids.transitionInvalid)).toHaveCount(0);
  await expect(rows.nth(1).getByTestId(ids.transitionInvalid)).toHaveText('Invalid');
  await expect(rows.nth(1).getByTestId(ids.transitionInvalidReason)).toContainText(
    'Review was removed',
  );
  await expect(rows.nth(2).getByTestId(ids.transitionInvalidReason)).toContainText(
    'Gone was removed',
  );
  // a Removed-category state the server still lists is live, so the transition naming it is valid
  await expect(rows.nth(3).getByTestId(ids.transitionInvalid)).toHaveCount(0);
});

test('AutomationsPane_WhenSaved_ShouldPutTheTransitionsAndKeepTheRest', async ({ mount, page }) => {
  const puts = await stubBoard(page, board('stories', [transition('t1', story.new, story.idea)]));
  await mountDialog(mount, 'stories');

  await page.getByTestId(ids.transitionAddTo).selectOption({ label: 'Active' });
  await page.getByTestId(ids.transitionAdd).click();
  await page.getByTestId(dialogIds.save).click();

  await expect.poll(() => puts.length).toBe(1);
  const sent = puts[0].settings.transitions;
  expect(sent).toHaveLength(2);
  expect(sent[0]).toEqual(transition('t1', story.new, story.idea));
  expect(sent[1]).toMatchObject({
    type: 'story',
    from: null,
    to: { stateId: 's-active', name: 'Active' },
  });
  expect(sent[1].actions).toEqual([
    { id: expect.any(String), kind: 'agent', prompt: '', connectionId: null },
  ]);
  expect(puts[0].basedOn.transitions).toHaveLength(1);
});

test('AutomationsPane_WhenReadOnly_ShouldDisableEveryControl', async ({ mount, page }) => {
  const dto = board('stories', [transition('t1', story.new, story.idea, 'Review it\nmore')]);
  await stubProject(page);
  await mount(
    <AutomationsPane
      draft={dto.settings}
      onChange={() => undefined}
      readOnly
      context={{
        projectId: PROJECT_ID,
        boardId: BOARD_ID,
        level: 'stories',
        states: dto.states,
        warnings: [],
      }}
    />,
  );
  await expect(page.getByTestId(ids.transitionSummary)).toHaveText('Send to agent: Review it');
  await expect(page.getByTestId(ids.transitionDelete)).toBeDisabled();
  await expect(page.getByTestId(ids.transitionAdd)).toBeDisabled();
  await expect(page.getByTestId(ids.transitionAddFrom)).toBeDisabled();
  await expect(page.getByTestId(ids.transitionAddTo)).toBeDisabled();
  await expect(page.getByTestId(ids.transitionAddType)).toBeDisabled();
  await expect(page.getByTestId(ids.actionPrompt)).toHaveAttribute('readonly', '');
  await expect(page.getByTestId(ids.actionConnection)).toBeDisabled();
  await expect(page.getByTestId(ids.actionAdd)).toHaveCount(0);
  await expect(page.getByTestId(ids.actionRemove)).toBeDisabled();
});

const withTwoActions = (): TransitionJson => ({
  ...transition('t1', story.new, story.idea, 'first'),
  actions: [
    { id: 'a1', kind: 'agent', prompt: 'first', connectionId: null },
    { id: 'a2', kind: 'agent', prompt: 'second', connectionId: null },
  ],
});

test('AutomationsPane_WhenPromptEdited_ShouldPreviewTheServersRenderAndSave', async ({
  mount,
  page,
}) => {
  const puts = await stubBoard(page, board('stories', [transition('t1', story.new, story.idea)]));
  await page.route('**/api/markdown/render', (route) => {
    const { markdown } = route.request().postDataJSON() as { markdown: string };
    return route.fulfill({ json: { markdown, html: `<p>rendered:${markdown}</p>` } });
  });
  await mountDialog(mount, 'stories');

  await expect(page.getByTestId(ids.actionPreview)).toContainText('Nothing yet');
  await page.getByTestId(ids.actionPrompt).fill('Review **it**');
  await expect(page.getByTestId(ids.actionPreview)).toContainText('rendered:Review **it**');
  await expect(page.getByTestId(ids.transitionSummary)).toHaveText('Send to agent: Review **it**');
  await page.getByTestId(dialogIds.save).click();
  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0].settings.transitions[0].actions[0].prompt).toBe('Review **it**');
});

test('AutomationsPane_WhenConnectionPickedAndCleared_ShouldSaveIdThenNull', async ({
  mount,
  page,
}) => {
  const puts = await stubBoard(page, board('stories', [transition('t1', story.new, story.idea)]));
  await mountDialog(mount, 'stories');

  const select = page.getByTestId(ids.actionConnection);
  await expect(select.locator('option')).toHaveText([
    'Project default (Home agent)',
    'Home agent',
    'Cloud agent',
  ]);
  await select.selectOption({ label: 'Cloud agent' });
  await page.getByTestId(dialogIds.save).click();
  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0].settings.transitions[0].actions[0].connectionId).toBe(CONN_B);

  await select.selectOption({ label: 'Project default (Home agent)' });
  await page.getByTestId(dialogIds.save).click();
  await expect.poll(() => puts.length).toBe(2);
  expect(puts[1].settings.transitions[0].actions[0].connectionId).toBeNull();
});

test('AutomationsPane_WhenActionRemoved_ShouldKeepTheLastOne', async ({ mount, page }) => {
  await stubBoard(page, board('stories', [withTwoActions()]));
  await mountDialog(mount, 'stories');

  await expect(page.getByTestId(ids.actionRow)).toHaveCount(2);
  await expect(page.getByTestId(ids.actionLastHint)).toHaveCount(0);
  await page.getByTestId(ids.actionRemove).first().click();
  await expect(page.getByTestId(ids.actionRow)).toHaveCount(1);
  await expect(page.getByTestId(ids.actionPrompt)).toHaveValue('second');
  await expect(page.getByTestId(ids.actionRemove)).toBeDisabled();
  await expect(page.getByTestId(ids.actionLastHint)).toBeVisible();

  // Send to agent is the only action kind and one runs per transition, so no second one is offered
  await expect(page.getByTestId(ids.actionAdd)).toHaveCount(0);
});

test('AutomationsPane_WhenTransitionHasNoAction_ShouldOfferAddSendToAgent', async ({
  mount,
  page,
}) => {
  await stubBoard(
    page,
    board('stories', [{ ...transition('t1', story.new, story.idea), actions: [] }]),
  );
  await mountDialog(mount, 'stories');
  await expect(page.getByTestId(ids.actionAdd)).toBeVisible();
  await page.getByTestId(ids.actionAdd).click();
  await expect(page.getByTestId(ids.actionRow)).toHaveCount(1);
  await expect(page.getByTestId(ids.actionAdd)).toHaveCount(0);
});

const withConnection = (): TransitionJson => {
  const t = transition('t1', story.new, story.idea);
  return { ...t, actions: [{ ...t.actions[0], connectionId: CONN_B }] };
};

test('AutomationsPane_WhenConnectionsAreLoading_ShouldDisableTheSelectAndShowLoading', async ({
  mount,
  page,
}) => {
  await stubBoard(page, board('stories', [withConnection()]));
  await page.route(`**/api/projects/${PROJECT_ID}/settings`, () => new Promise(() => undefined));
  await mountDialog(mount, 'stories');

  const select = page.getByTestId(ids.actionConnection);
  await expect(select).toBeDisabled();
  await expect(select.locator('option:checked')).toHaveText('Loading…');
});

test('AutomationsPane_WhenConnectionsFailToLoad_ShouldKeepTheSavedConnectionAndSaveIt', async ({
  mount,
  page,
}) => {
  const puts = await stubBoard(page, board('stories', [withConnection()]));
  await page.route(`**/api/projects/${PROJECT_ID}/settings`, (route) =>
    route.fulfill({ status: 500, json: { error: 'boom' } }),
  );
  await mountDialog(mount, 'stories');

  const select = page.getByTestId(ids.actionConnection);
  await expect(select.locator('option:checked')).toHaveText("Couldn't load connections");
  await expect(select).toBeDisabled();
  await page.getByTestId(ids.actionPrompt).fill('edited');
  await page.getByTestId(dialogIds.save).click();
  await expect.poll(() => puts.length).toBe(1);
  expect(puts[0].settings.transitions[0].actions[0].connectionId).toBe(CONN_B);
});

test('AutomationsPane_WhenPromptOver20000Characters_ShouldShowTheLimitMessage', async ({
  mount,
  page,
}) => {
  await stubBoard(page, board('stories', [transition('t1', story.new, story.idea)]));
  await mountDialog(mount, 'stories');

  await page.getByTestId(ids.actionPrompt).fill('x'.repeat(20_000));
  await expect(page.getByTestId(ids.actionPromptError)).toHaveCount(0);
  await page.getByTestId(ids.actionPrompt).fill('x'.repeat(20_001));
  await expect(page.getByTestId(ids.actionPromptError)).toHaveText(
    'A prompt is at most 20,000 characters. This one is 20,001.',
  );
  await expect(page.getByTestId(ids.actionPrompt)).toHaveAttribute('aria-invalid', 'true');
});
