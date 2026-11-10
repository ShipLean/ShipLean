// The title an action item starts with: the point's text, cut as the web cuts it (RetroActionForm.tsx, TITLE_SEED_LENGTH).
export const TITLE_SEED_LENGTH = 80;

// Word for word, never reworded or shortened by a model (D7). A cut never splits a surrogate pair.
export function seedTitle(text: string): string {
  const cut = text.slice(0, TITLE_SEED_LENGTH);
  const last = cut.charCodeAt(cut.length - 1);
  return last >= 0xd800 && last <= 0xdbff && text.length > cut.length ? cut.slice(0, -1) : cut;
}
