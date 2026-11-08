import { describe, expect, it } from 'vitest';
import { pageInView, pageWidth, scrollTarget } from './pagerScroll';

describe('pagerScroll', () => {
  it('pageInView_WhenScrolledToTheSecondPage_ShouldBeIt', () => {
    expect(pageInView(340, 340, 8, 4)).toBe(1);
  });

  it('pageInView_WhenBetweenPages_ShouldBeTheNearest', () => {
    expect(pageInView(100, 340, 8, 4)).toBe(0);
    expect(pageInView(200, 340, 8, 4)).toBe(1);
    expect(pageInView(5000, 340, 8, 4)).toBe(3);
  });

  it('pageWidth_WhenOnAPhone_ShouldBeAtMost340', () => {
    expect(pageWidth(390)).toBe(340);
    expect(pageWidth(375)).toBe(327);
    expect(pageWidth(768)).toBe(340);
  });

  it('scrollTarget_WhenPastTheEnd_ShouldStopAtTheMostTheTrackScrolls', () => {
    expect(scrollTarget(2, 340, 8, 292)).toBe(292);
    expect(scrollTarget(1, 340, 8, 1000)).toBe(348);
  });
});
