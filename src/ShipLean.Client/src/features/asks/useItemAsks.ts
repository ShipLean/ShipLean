import { useQuery } from '@tanstack/react-query';
import { asksKey, fetchAsks } from './asksApi';

// The one asks request per work item page. Regions never fetch: they get the result through props (regions.ts).
export function useItemAsks(projectId: string, itemKey: string, workItemId: string) {
  return useQuery({
    queryKey: asksKey(projectId, itemKey),
    queryFn: () => fetchAsks(workItemId),
  });
}
