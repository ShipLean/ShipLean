import { useQuery, useQueryClient } from '@tanstack/react-query';
import { featureBoardKeys, getFeatureBoard } from '@shiplean/core/api/featureBoard';
import {
  featuresKeys,
  getFeatureDigest,
  getFeaturesOverview,
} from '@shiplean/core/api/featuresOverview';
import { getStates, projectMetaKeys } from '@shiplean/core/api/projectMeta';

export function useFeaturesOverviewQuery(projectId: string) {
  return useQuery({
    queryKey: featuresKeys.overview(projectId),
    queryFn: () => getFeaturesOverview(projectId),
  });
}

export function useFeatureDigestQuery(projectId: string, cursor: string | null) {
  return useQuery({
    queryKey: featuresKeys.digest(projectId, cursor),
    queryFn: () => getFeatureDigest(projectId, cursor),
  });
}

// Shares its key with the work item lane's board query, so a change in one area refreshes the other.
export function useFeatureBoardQuery(projectId: string, featureKey: string) {
  return useQuery({
    queryKey: featureBoardKeys.board(projectId, featureKey),
    queryFn: () => getFeatureBoard(projectId, featureKey),
  });
}

export function useFeatureStatesQuery(projectId: string) {
  return useQuery({
    queryKey: projectMetaKeys.states(projectId),
    queryFn: () => getStates(projectId),
  });
}

// The overview and digest. The board (featureBoardKeys.board) is keyed separately; invalidate it too after a write that
// shows on the board.
export function useInvalidateFeatures() {
  const client = useQueryClient();
  return (projectId: string) =>
    client.invalidateQueries({ queryKey: featuresKeys.root(projectId) });
}
