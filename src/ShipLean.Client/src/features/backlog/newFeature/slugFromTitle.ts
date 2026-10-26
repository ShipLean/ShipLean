// The suggestion typed into the Slug field. Mirrors the server's Slugs.FromTitle (so a suggested slug is always valid):
// accents are dropped, any other run of characters that aren't ASCII letters or digits becomes one hyphen, at most 64
// characters with no trailing hyphen.
export const SLUG_MAX_LENGTH = 64;

export function slugFromTitle(title: string): string {
  const slug = title
    .normalize('NFD')
    .replace(/\p{Mn}/gu, '')
    .replace(/[^A-Za-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .toLowerCase();
  return slug.length <= SLUG_MAX_LENGTH ? slug : slug.slice(0, SLUG_MAX_LENGTH).replace(/-+$/, '');
}
