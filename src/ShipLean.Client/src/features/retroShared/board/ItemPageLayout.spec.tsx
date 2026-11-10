import { expect, test } from '@playwright/experimental-ct-react';
import type { ProjectDto } from '@shiplean/core/api/projects';
import type { RetroItemDto } from '@shiplean/core/api/retros';
import { retroItem, stubRetros } from '../../retros/ct/retroFixtures';
import { RetroItemPanel } from '../../retros/item/RetroItemPanel';
import { retroSharedTestIds as ids } from '../testIds';

const project = { id: 'p1', key: 'SHIP' } as ProjectDto;
const route = { hooksConfig: { route: '/p/SHIP/retros/items/SHIP-101' } };

/** The fixture item with every column grown well past the window. */
const tall: RetroItemDto = {
  ...retroItem,
  categories: retroItem.categories.map((column) => ({
    ...column,
    groups: column.groups.map((group) => ({
      ...group,
      points: Array.from({ length: 8 }, (_, copy) =>
        group.points.map((point) => ({ ...point, id: `${point.id}-${copy}` })),
      ).flat(),
    })),
  })),
};

for (const [width, height] of [
  [1280, 720],
  [1440, 800],
] as const) {
  test(`ItemPageLayout_At${width}WithATallBoardAndAnEditorOpen_ShouldNotOverflowAndKeepTheBarsInView`, async ({
    mount,
    page,
  }) => {
    await page.setViewportSize({ width, height });
    await stubRetros(page);
    await page.route(/\/api\/projects\/[^/]+\/retros\/items\/[^/]+$/, (r) =>
      r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(tall) }),
    );
    await mount(<RetroItemPanel project={project} itemKey="SHIP-101" />, route);
    const board = page.getByTestId(ids.retroBoard);
    await expect(board).toBeVisible();
    const boardHeight = (await board.boundingBox())!.height;
    expect(boardHeight).toBeGreaterThan(height);

    const add = page.getByTestId(ids.retroBoardAdd).first();
    await add.scrollIntoViewIfNeeded();
    await add.click();
    await expect(page.getByTestId(ids.retroBoardAddText)).toBeVisible();
    await page.evaluate(() => window.scrollTo(0, 0));

    const overflow = await page.evaluate(() => ({
      page: document.documentElement.scrollWidth - document.documentElement.clientWidth,
      board:
        document.querySelector('[data-testid="retro-board"]')!.scrollWidth -
        document.querySelector('[data-testid="retro-board"]')!.clientWidth,
    }));
    expect(overflow).toEqual({ page: 0, board: 0 });

    const buttons = page.getByRole('button');
    expect(await buttons.count()).toBeGreaterThan(0);
    const inView = await buttons.evaluateAll(
      (els, limit) =>
        els
          .filter((el) => !el.closest('[data-testid="retro-board"]'))
          .map((el) => el.getBoundingClientRect())
          .every((r) => r.bottom <= limit && r.right <= window.innerWidth),
      height,
    );
    expect(inView).toBe(true);
    const bars = await page.getByRole('button').first().boundingBox();
    expect(bars!.y + bars!.height).toBeLessThanOrEqual(height);
  });
}
