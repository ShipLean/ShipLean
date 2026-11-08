import { describe, expect, it } from 'vitest';
import { shouldCloseOnRelease } from './swipe';

describe('shouldCloseOnRelease', () => {
  it('shouldCloseOnRelease_WhenDraggedPastTheThreshold_ShouldBeTrue', () => {
    expect(shouldCloseOnRelease(80, 2000)).toBe(true);
    expect(shouldCloseOnRelease(200, 1000)).toBe(true);
  });

  it('shouldCloseOnRelease_WhenFlickedFast_ShouldBeTrue', () => {
    expect(shouldCloseOnRelease(40, 50)).toBe(true);
    // Just over 0.5 px/ms.
    expect(shouldCloseOnRelease(40, 79)).toBe(true);
  });

  it('shouldCloseOnRelease_WhenShortAndSlow_ShouldBeFalse', () => {
    expect(shouldCloseOnRelease(79, 400)).toBe(false);
    expect(shouldCloseOnRelease(30, 600)).toBe(false);
    // Just under 0.5 px/ms.
    expect(shouldCloseOnRelease(40, 81)).toBe(false);
    expect(shouldCloseOnRelease(-200, 50)).toBe(false);
    expect(shouldCloseOnRelease(0, 0)).toBe(false);
  });
});
