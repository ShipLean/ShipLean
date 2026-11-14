import { expect, test } from '@playwright/experimental-ct-react';
import { PhoneProbe } from './ct/PhoneProbe';

// Pins the CT phone project (K8): 390×844 with touch, the touch query matching, and phone baselines in this spec's own
// snapshot folder.
test('PhoneProject_ShouldBe390By844WithTouch', async ({ mount, page }) => {
  const probe = await mount(<PhoneProbe />);
  await expect(probe).toBeVisible();

  const viewport = await page.evaluate(() => ({
    width: window.innerWidth,
    height: window.innerHeight,
    touchPoints: navigator.maxTouchPoints,
    coarse: window.matchMedia('(pointer: coarse)').matches,
    touchQuery: window.matchMedia('(max-width: 900px)').matches,
  }));
  expect(viewport).toEqual({
    width: 390,
    height: 844,
    touchPoints: expect.any(Number),
    coarse: true,
    touchQuery: true,
  });
  expect(viewport.touchPoints).toBeGreaterThan(0);
  expect(page.viewportSize()).toEqual({ width: 390, height: 844 });

  expect(test.info().snapshotPath('probe.png')).toMatch(
    /[\\/]PhoneProject\.phone\.spec\.tsx-snapshots[\\/]probe/,
  );
});
