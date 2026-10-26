import { useItemMarkdown } from '../../../feature/featureApi';
import { useBeforeSave } from '../beforeSave';
import { useDebouncedValue } from './useDebouncedValue';

const RENDER_PAUSE_MS = 300;

/**
 * The external images the draft's rendering lists (never loaded: the server shows a placeholder), and the Save check
 * that asks before they go in. Only a changed draft is rendered (after a pause in typing, so it is not one request per
 * keystroke); the saved text needs no warning. While the render for the current text is pending or failed, Save asks anyway.
 */
export function useExternalImages(
  value: string,
  changed: boolean,
  workItemId: string | undefined,
  title: string,
): string[] {
  const field = title.toLowerCase();
  const settled = useDebouncedValue(value, RENDER_PAUSE_MS);
  const active = workItemId !== undefined && changed && value.trim() !== '';
  const rendered = useItemMarkdown(settled, workItemId, active && settled === value);
  const failed = active && settled === value && rendered.isError;
  const unchecked = active && (settled !== value || !rendered.isSuccess);
  const external =
    workItemId !== undefined && changed
      ? (rendered.data?.images ?? []).filter((image) => image.status === 'external')
      : [];
  const sources = external.map((image) => image.src);
  useBeforeSave(() =>
    failed
      ? { confirm: `Images in the ${field} couldn't be checked.` }
      : unchecked
        ? { confirm: `Images in the ${field} are still being checked.` }
        : sources.length === 0
          ? null
          : {
              confirm: `The ${field} links to external images that will not be shown: ${sources.join(', ')}.`,
            },
  );
  return sources;
}
