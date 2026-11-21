import { skipToken, useQuery } from '@tanstack/react-query';
import type { PromptsPageDto } from '@shiplean/core/api/prompts';
import { promptsTestIds as ids } from './testIds';
import { promptsPageKey } from './usePrompts';

// What the Prompts tab shows at its right end: "N custom" once the page has loaded and any prompt is custom. It only reads
// the page query's cache (skipToken: it never fetches), so the other tabs make no request; the count appears after the
// Prompts tab has been opened once and follows every save, which refetches the page there.
export function PromptsTabBadge({ projectId }: { projectId: string }) {
  const { data } = useQuery<PromptsPageDto>({
    queryKey: promptsPageKey(projectId),
    queryFn: skipToken,
  });
  const count = data?.customCount ?? 0;
  if (count === 0) return null;
  return (
    <span
      data-testid={ids.tabBadge}
      className="ml-1.5 text-[length:var(--text-xs)] font-medium text-label-purple"
    >
      {count} custom
    </span>
  );
}
