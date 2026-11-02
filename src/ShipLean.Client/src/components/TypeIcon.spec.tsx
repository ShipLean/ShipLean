import { expect, test } from '@playwright/experimental-ct-react';
import { TypeIcon, type WorkItemType } from './TypeIcon';
import { componentTestIds as ids } from './testIds';

const types: WorkItemType[] = ['epic', 'feature', 'story', 'bug', 'task', 'action'];

test('TypeIcon_ShouldRenderEachType', async ({ mount, page }) => {
  await mount(
    <div>
      {types.map((type) => (
        <TypeIcon key={type} type={type} />
      ))}
    </div>,
  );
  const icons = page.getByTestId(ids.typeIcon);
  await expect(icons).toHaveCount(6);
  for (const [index, type] of types.entries()) {
    await expect(icons.nth(index)).toHaveAttribute('data-type', type);
  }
  await expect(icons.nth(3)).toHaveText('B');
  await expect(icons.nth(5)).toHaveText('A');
});

test('TypeIcon_WhenAction_ShouldUseTheActionColourInLightAndDark', async ({ mount, page }) => {
  await mount(<TypeIcon type="action" />);
  const icon = page.getByTestId(ids.typeIcon);
  const colourOf = () =>
    icon.evaluate((node) => {
      const probe = document.createElement('span');
      probe.style.color = 'var(--type-action)';
      document.body.append(probe);
      const colour = getComputedStyle(probe).color;
      probe.remove();
      return { colour, background: getComputedStyle(node).backgroundColor };
    });

  await page.emulateMedia({ colorScheme: 'light' });
  const light = await colourOf();
  expect(light.background).toBe(light.colour);
  expect(light.colour).toBe('rgb(31, 143, 106)');

  await page.emulateMedia({ colorScheme: 'dark' });
  const dark = await colourOf();
  expect(dark.background).toBe(dark.colour);
  expect(dark.colour).toBe('rgb(76, 199, 154)');
});
