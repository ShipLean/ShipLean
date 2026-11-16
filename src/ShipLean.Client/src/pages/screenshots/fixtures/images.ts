// Small PNGs for the images a page loads from the API (profile photos, link-card previews and favicons), so screenshots
// show a picture rather than the browser's broken-image glyph. Pinned for the whole of SHIP-1626 (K6): add new images
// beside these, never change these, or every shot that shows one moves.

/** 64×64: a head and shoulders on teal, so it reads as a photo rather than the initials' flat colour. */
export const photoPng =
  'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAIAAAAlC+aJAAAAzElEQVR42u3YyxHCMAxF0VcXtVFC1qmFkiiDLQHifCziJ/vOqIB7FpnI0u0+pR4BAAAAAAAAAAAAALCY52NeG3dAIf1PDF2cHs5Qq/oogxrWhxiGB1TW1xvGBoTUVxoAAAAAAAAAVonMANbpmRcZb+JOrhKd3IU4LTYFXP8Fj/0fCNzkaiQyqT9tkEn6aYbc6o8a5JZ+lCHb+p0GOdfvMci8ftMg//qyoV+AVX3BoCz1a4YeAbb1Pw3KVf9tAGAFSFH/YQDgA0hU/254AUK622DD0oSpAAAAAElFTkSuQmCC';

/** 240×126 (a link preview's og:image shape): sky over hills. */
export const previewPng =
  'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAPAAAAB+CAIAAACVjQttAAABlElEQVR42u3SURECMRBEwXGIAByhAUGIQcT9IYC7ZDe5rmoFMy/vzxe2ERMgaBA0CBoEjaBB0CBoEDQIGkGDoEHQIGgQNIIGQYOgQdAgaAQNggZBg6BB0AgaBA2CBkGDoBE0CBoEDYIGQSNoEDQIGgSNoEHQIGgQNAgaQYOgQdAgaBA0ggZBg6BB0CBoBA2CBkGDoEHQCBrWCvrxelphAjvPC9rWE2q285yd89va3ENTtvOcnWPryTXbeejOMffklO08dOeYuyRlOw/aObaurdnO1+4cc9embOdrd465O6Rs56t2jrn7pGzn8zvH3N1StvOZnWPuninb+T+xeNuO7VwW9MZzN0nZzgVB77R4w47tXBb0uosv0bGdy4JeZfFFO7ZzWdA9F9+mYzuXBV27+8YF27lF0KN3v23Bt9053mUngkbQIGgQNAgaQYOgQdAgaBA0ggZBg6BB0CBoBA2CBkGDoEHQCBoEDYIGQYOgETQIGgQNggZBI2gQNAgaBA2CRtAgaBA0CBpBWwFBg6BB0CBo7uoAUZHphngN6nEAAAAASUVORK5CYII=';

/** 16×16: an orange square with a white bar. */
export const faviconPng =
  'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAIAAACQkWg2AAAAGklEQVR42mN4kCNPEmIYVhr+4wWjGkZc0gAA3ZjJSCQ3qJ8AAAAASUVORK5CYII=';

// The part of Playwright's Page and Route these stubs use (the CT package does not re-export the types).
interface ImageRoute {
  fulfill(response: { status: number; contentType: string; body: Buffer }): Promise<void>;
}
interface Page {
  route(url: string | RegExp, handler: (route: ImageRoute) => Promise<void>): Promise<unknown>;
}

const png = (dataUri: string) => ({
  status: 200,
  contentType: 'image/png',
  body: Buffer.from(dataUri.slice(dataUri.indexOf(',') + 1), 'base64'),
});

/** Answers `/api/users/{id}/photo`, `/api/link-cards/{id}/image` and `/api/link-cards/{id}/favicon` with the PNGs above.
 * Register it after a spec's other stubs: stubs registered later win. */
export async function stubImages(page: Page) {
  await page.route('**/api/users/*/photo*', (route) => route.fulfill(png(photoPng)));
  await page.route('**/api/link-cards/*/image', (route) => route.fulfill(png(previewPng)));
  await page.route('**/api/link-cards/*/favicon', (route) => route.fulfill(png(faviconPng)));
}
