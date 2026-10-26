import { expect, test } from '@playwright/experimental-ct-react';
import { TypeIcon, type WorkItemType } from './TypeIcon';
import { componentTestIds as ids } from './testIds';

const types: WorkItemType[] = ['epic', 'feature', 'story', 'bug', 'task'];

test('TypeIcon_ShouldRenderEachType', async ({ mount, page }) => {
  await mount(
    <div>
      {types.map((type) => (
        <TypeIcon key={type} type={type} />
      ))}
    </div>,
  );
  const icons = page.getByTestId(ids.typeIcon);
  await expect(icons).toHaveCount(5);
  for (const [index, type] of types.entries()) {
    await expect(icons.nth(index)).toHaveAttribute('data-type', type);
  }
  await expect(icons.nth(3)).toHaveText('B');
});
