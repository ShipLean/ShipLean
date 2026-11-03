import { expect, test } from '@playwright/experimental-ct-react';
import { AppRoutes } from '../../../AppRoutes';
import { stubShell } from '../../../pages/screenshots/shellStub';
import { stubRetros } from '../ct/retroFixtures';
import { retroSharedTestIds as shared } from '../../retroShared/testIds';
import { retrosTestIds as ids } from '../testIds';

for (const width of [1280, 1440]) {
  test(`SprintRetro_WhenByWorkItemAt${width}_ShouldKeepEachHeaderInsideItsBoardAndNotScrollSideways`, async ({
    mount,
    page,
  }) => {
    await page.setViewportSize({ width, height: 600 });
    await stubShell(page);
    await stubRetros(page);
    await mount(<AppRoutes />, {
      hooksConfig: { route: '/p/SHIP/sprints/sprint-24/retro?view=item' },
    });
    const cards = page.getByTestId(shared.retroCard);
    await expect(page.getByTestId(ids.retroCardOpen).first()).toBeVisible();
    const found = await cards.evaluateAll((nodes) => ({
      count: nodes.length,
      listHeight: nodes.reduce((sum, n) => sum + n.getBoundingClientRect().height, 0),
      innerHeight: window.innerHeight,
      horizontal: document.documentElement.scrollWidth - document.documentElement.clientWidth,
    }));
    expect(found.listHeight).toBeGreaterThan(found.innerHeight);
    expect(found.horizontal).toBeLessThanOrEqual(0);
    for (const card of await cards.all()) {
      const outer = (await card.boundingBox())!;
      const header = (await card.getByTestId(ids.retroCardHeader).boundingBox())!;
      expect(header.x).toBeGreaterThanOrEqual(outer.x - 1);
      expect(header.x + header.width).toBeLessThanOrEqual(outer.x + outer.width + 1);
      expect(outer.x + outer.width).toBeLessThanOrEqual(width);
      for (const id of [ids.retroCardProgress, ids.retroCardOpen]) {
        const part = card.getByTestId(id);
        if ((await part.count()) === 0) continue;
        const box = (await part.boundingBox())!;
        expect(box.x + box.width).toBeLessThanOrEqual(outer.x + outer.width + 1);
      }
    }
    await page.screenshot({ path: `/tmp/claude-1000/sprint-retro-item-${width}.png` });
  });
}
