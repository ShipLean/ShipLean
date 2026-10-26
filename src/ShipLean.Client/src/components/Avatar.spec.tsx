import { expect, test } from '@playwright/experimental-ct-react';
import { Avatar } from './Avatar';
import { componentTestIds as ids } from './testIds';

const photo =
  'data:image/svg+xml;utf8,<svg xmlns="http://www.w3.org/2000/svg" width="4" height="4"><rect width="4" height="4" fill="red"/></svg>';

test('Avatar_WhenNoPerson_ShouldRenderDashed', async ({ mount, page }) => {
  await mount(<Avatar initials={null} />);
  await expect(page.getByTestId(ids.avatar)).toHaveAttribute('data-variant', 'none');
  await expect(page.getByTestId(ids.avatar)).toHaveCSS('border-top-style', 'dashed');
});

test('Avatar_WhenPhotoGiven_ShouldShowThePhoto', async ({ mount, page }) => {
  await mount(<Avatar initials="JD" avatarColour="#6f45bf" photoUrl={photo} />);
  await expect(page.getByTestId(ids.avatarPhoto)).toBeVisible();
});

test('Avatar_WhenPersonGiven_ShouldShowInitials', async ({ mount, page }) => {
  await mount(<Avatar initials="JD" avatarColour="#3f7d5a" />);
  await expect(page.getByTestId(ids.avatar)).toHaveText('JD');
  await expect(page.getByTestId(ids.avatar)).toHaveAttribute('data-variant', 'person');
});
