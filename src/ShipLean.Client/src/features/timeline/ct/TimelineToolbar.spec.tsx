import { expect, test } from '@playwright/experimental-ct-react';
import { DEFAULT_TIMELINE_OPTIONS, type TimelineOptions } from '../TimelineOptions';
import { timelineTestIds as ids } from '../testIds';
import { TimelineToolbar } from '../TimelineToolbar';

const changes = (): { seen: TimelineOptions[]; onChange: (next: TimelineOptions) => void } => {
  const seen: TimelineOptions[] = [];
  return { seen, onChange: (next) => seen.push(next) };
};

test('TimelineToolbar_WhenGroupChanged_ShouldWriteQuery', async ({ mount, page }) => {
  const { seen, onChange } = changes();
  await mount(<TimelineToolbar options={DEFAULT_TIMELINE_OPTIONS} finished onChange={onChange} />);
  await page.getByTestId(ids.toolbarGroup).getByRole('button', { name: 'People' }).click();
  expect(seen).toEqual([{ ...DEFAULT_TIMELINE_OPTIONS, group: 'people' }]);
});

test('TimelineToolbar_WhenZoomChanged_ShouldWriteQuery', async ({ mount, page }) => {
  const { seen, onChange } = changes();
  await mount(<TimelineToolbar options={DEFAULT_TIMELINE_OPTIONS} finished onChange={onChange} />);
  await page.getByTestId(ids.toolbarZoom).getByRole('button', { name: 'Days' }).click();
  expect(seen).toEqual([{ ...DEFAULT_TIMELINE_OPTIONS, zoom: 'days' }]);
});

test('TimelineToolbar_WhenDepsChanged_ShouldWriteQuery', async ({ mount, page }) => {
  const { seen, onChange } = changes();
  await mount(<TimelineToolbar options={DEFAULT_TIMELINE_OPTIONS} finished onChange={onChange} />);
  await page.getByTestId(ids.toolbarDeps).click();
  expect(seen).toEqual([{ ...DEFAULT_TIMELINE_OPTIONS, deps: false }]);
});

test('TimelineToolbar_WhenCpChanged_ShouldWriteQuery', async ({ mount, page }) => {
  const { seen, onChange } = changes();
  await mount(<TimelineToolbar options={DEFAULT_TIMELINE_OPTIONS} finished onChange={onChange} />);
  await page.getByTestId(ids.toolbarCp).click();
  expect(seen).toEqual([{ ...DEFAULT_TIMELINE_OPTIONS, cp: false }]);
});

test('TimelineToolbar_WhenViewChanged_ShouldWriteQuery', async ({ mount, page }) => {
  const { seen, onChange } = changes();
  await mount(<TimelineToolbar options={DEFAULT_TIMELINE_OPTIONS} finished onChange={onChange} />);
  await page.getByTestId(ids.toolbarView).getByRole('button', { name: 'JSON for agents' }).click();
  expect(seen).toEqual([{ ...DEFAULT_TIMELINE_OPTIONS, view: 'json' }]);
});

test('TimelineToolbar_WhenNotFinished_ShouldDisableCriticalPath', async ({ mount, page }) => {
  await mount(
    <TimelineToolbar options={DEFAULT_TIMELINE_OPTIONS} finished={false} onChange={() => {}} />,
  );
  await expect(page.getByTestId(ids.toolbarCp)).toBeDisabled();
  await expect(page.getByTestId(ids.toolbarDeps)).toBeEnabled();
  await expect(page.getByTestId(ids.toolbarLegend)).not.toContainText('Critical path');
});

test('TimelineToolbar_ShouldKeepTheOptionAttributesOnItsRoot', async ({ mount, page }) => {
  await mount(
    <TimelineToolbar
      options={{ group: 'tasks', zoom: 'days', deps: false, cp: false, view: 'json' }}
      finished
      onChange={() => {}}
    />,
  );
  const root = page.getByTestId(ids.toolbar);
  await expect(root).toHaveAttribute('data-group', 'tasks');
  await expect(root).toHaveAttribute('data-zoom', 'days');
  await expect(root).toHaveAttribute('data-deps', 'false');
  await expect(root).toHaveAttribute('data-cp', 'false');
  await expect(root).toHaveAttribute('data-view', 'json');
});
