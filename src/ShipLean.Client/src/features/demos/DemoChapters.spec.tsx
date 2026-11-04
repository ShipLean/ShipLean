import { expect, test } from '@playwright/experimental-ct-react';
import ready from '../../../../ShipLean.TestSupport/DtoFixtures/demos/sprint-demo-ready.json' with { type: 'json' };
import unlisted from '../../../../ShipLean.TestSupport/DtoFixtures/demos/sprint-demo-youtube-unlisted.json' with { type: 'json' };
import type { SprintDemoDto } from '../../api/demos';
import { DemoPlayerHarness } from './ct/DemoPlayerHarness';
import { demosTestIds as ids } from './testIds';

const readyDemo = ready as unknown as SprintDemoDto;
const unlistedDemo = unlisted as unknown as SprintDemoDto;
// The fixture json's second chapter starts at 4 s of the 8-second file.
const secondStart = readyDemo.chapters[1].startSeconds;

test('Chapters_WhenClicked_ShouldSeekTheVideo', async ({ mount }) => {
  const component = await mount(<DemoPlayerHarness demo={readyDemo} />);
  const video = component.getByTestId(ids.demoVideo);
  await video.evaluate((element: HTMLVideoElement) => {
    element.addEventListener('seeked', () => {
      element.dataset.seekedAt = String(element.currentTime);
    });
  });

  await component.getByTestId(ids.demoChapter).nth(1).click();

  await expect(video).toHaveAttribute('data-seeked-at', /\d/);
  const seekedAt = Number(await video.getAttribute('data-seeked-at'));
  expect(seekedAt).toBeGreaterThan(secondStart - 0.5);
  expect(seekedAt).toBeLessThan(secondStart + 0.5);
});

test('Chapters_WhenPlaying_ShouldMarkTheCurrentOne', async ({ mount }) => {
  const component = await mount(<DemoPlayerHarness demo={readyDemo} />);
  const chapters = component.getByTestId(ids.demoChapter);
  await expect(chapters.nth(0)).toHaveAttribute('aria-current', 'true');
  await expect(chapters.nth(1)).toHaveAttribute('aria-current', 'false');

  await component.getByTestId(ids.demoVideo).evaluate((element: HTMLVideoElement) => {
    element.currentTime = 5;
  });

  await expect(chapters.nth(1)).toHaveAttribute('aria-current', 'true');
  await expect(chapters.nth(0)).toHaveAttribute('aria-current', 'false');
});

test('Chapters_WhenEmbed_ShouldSetStartOnTheIframe', async ({ mount }) => {
  const component = await mount(<DemoPlayerHarness demo={unlistedDemo} />);
  const embed = component.getByTestId(ids.demoEmbed);
  await expect(embed).not.toHaveAttribute('src', /start=/);

  await component.getByTestId(ids.demoChapter).nth(1).click();

  await expect(embed).toHaveAttribute(
    'src',
    `https://www.youtube-nocookie.com/embed/abc123XYZ_-?start=${Math.floor(secondStart)}&autoplay=1`,
  );
  await expect(component.getByTestId(ids.demoChapter).nth(1)).toHaveAttribute(
    'aria-current',
    'true',
  );
});

test('Layout_WhenFortyChaptersAndALongScript_ShouldStayInsideTheWindow', async ({
  mount,
  page,
}) => {
  const chapters = Array.from({ length: 40 }, (_, index) => ({
    title: `Chapter ${index + 1} with a rather long title that has to be cut off at the edge`,
    startSeconds: index * 0.2,
    startLabel: `0:0${index % 10}`,
    featureKey: index % 3 === 0 ? 'SHIP-101' : null,
  }));
  const scriptHtml = Array.from(
    { length: 30 },
    (_, index) =>
      `<h2>Chapter ${index + 1}</h2><p>${'The agent says a long sentence about the sprint. '.repeat(8)}</p>`,
  ).join('');
  const demo = { ...readyDemo, chapters, scriptHtml };
  for (const width of [1280, 1440]) {
    await page.setViewportSize({ width, height: 800 });
    const component = await mount(<DemoPlayerHarness demo={demo} />);

    const overflow = await page.evaluate(() => ({
      sideways: document.documentElement.scrollWidth - window.innerWidth,
    }));
    expect(overflow.sideways).toBeLessThanOrEqual(0);
    // The chapter list scrolls inside its box rather than stretching the page past the script.
    const list = await component.getByTestId(ids.demoChapters).boundingBox();
    expect(list?.height).toBeLessThanOrEqual(321);
    await component.unmount();
  }
});
