import { expect, test } from '@playwright/experimental-ct-react';
import { StateDot, type StateCategory } from './StateDot';
import { componentTestIds as ids } from './testIds';

const categories: StateCategory[] = ['proposed', 'inProgress', 'resolved', 'completed', 'removed'];

test('StateDot_ShouldRenderEachCategory', async ({ mount, page }) => {
  await mount(
    <div>
      {categories.map((category) => (
        <StateDot key={category} category={category} name={category} />
      ))}
    </div>,
  );
  const dots = page.getByTestId(ids.stateDot);
  await expect(dots).toHaveCount(5);
  for (const [index, category] of categories.entries()) {
    await expect(dots.nth(index)).toHaveAttribute('data-category', category);
  }
});
