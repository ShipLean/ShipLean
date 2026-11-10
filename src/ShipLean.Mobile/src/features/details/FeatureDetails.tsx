import type { FeatureRowDto } from '@shiplean/core/api/featuresOverview';
import { View } from 'react-native';
import { featureDetailsId } from '../testIds';

export interface FeatureDetailsProps {
  row: FeatureRowDto;
  projectId: string;
  projectKey: string;
}

// TODO: the expanded row from the design (status line, health, state, asks and actions).
export function FeatureDetails({ row }: FeatureDetailsProps) {
  return <View testID={featureDetailsId(row.key)} />;
}
