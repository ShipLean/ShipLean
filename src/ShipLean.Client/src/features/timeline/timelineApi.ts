import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';
import { featureTimelineKeys, getFeatureTimeline } from '@shiplean/core/api/featureTimeline';
import { registerInvalidation } from '../../realtime';

/** The Feature timeline view, read again when someone else's write moves or changes a work item. */
export function useFeatureTimeline(projectId: string, featureKey: string) {
  const queryClient = useQueryClient();
  useEffect(() => {
    const refetch = () =>
      void queryClient.invalidateQueries({
        queryKey: featureTimelineKeys.timeline(projectId, featureKey),
      });
    const stops = [
      registerInvalidation('workItemUpdated', refetch),
      registerInvalidation('workItemMoved', refetch),
    ];
    return () => stops.forEach((stop) => stop());
  }, [projectId, featureKey, queryClient]);
  return useQuery({
    queryKey: featureTimelineKeys.timeline(projectId, featureKey),
    queryFn: () => getFeatureTimeline(projectId, featureKey),
  });
}
