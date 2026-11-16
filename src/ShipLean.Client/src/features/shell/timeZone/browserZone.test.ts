import { afterEach, describe, expect, test, vi } from 'vitest';

// browserZone keeps its answer at module level, so each test loads it again, like a fresh page load.
async function freshBrowserZone() {
  vi.resetModules();
  return (await import('./browserZone')).browserZone;
}

afterEach(() => vi.restoreAllMocks());

describe('browserZone', () => {
  test('browserZone_WhenIntlThrows_ShouldGiveNullAndNotThrow', async () => {
    vi.spyOn(Intl, 'DateTimeFormat').mockImplementation(() => {
      throw new RangeError('no zone data');
    });
    const browserZone = await freshBrowserZone();

    expect(browserZone()).toBeNull();
  });

  test('browserZone_WhenIntlGivesNoZone_ShouldGiveNull', async () => {
    vi.spyOn(Intl, 'DateTimeFormat').mockImplementation(
      () => ({ resolvedOptions: () => ({ timeZone: '' }) }) as unknown as Intl.DateTimeFormat,
    );
    const browserZone = await freshBrowserZone();

    expect(browserZone()).toBeNull();
  });

  test('browserZone_WhenAskedAgain_ShouldAskIntlOnlyOnce', async () => {
    const spy = vi
      .spyOn(Intl, 'DateTimeFormat')
      .mockImplementation(
        () => ({ resolvedOptions: () => ({ timeZone: 'Asia/Bangkok' }) }) as unknown as Intl.DateTimeFormat,
      );
    const browserZone = await freshBrowserZone();

    expect([browserZone(), browserZone(), browserZone()]).toEqual([
      'Asia/Bangkok',
      'Asia/Bangkok',
      'Asia/Bangkok',
    ]);
    expect(spy).toHaveBeenCalledTimes(1);
  });
});
