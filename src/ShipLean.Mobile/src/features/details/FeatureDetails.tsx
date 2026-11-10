import type { FeatureRowDto } from '@shiplean/core/api/featuresOverview';
import { View } from 'react-native';
import { featureDetailsId } from '../testIds';

export interface FeatureDetailsProps {
  row: FeatureRowDto;
  projectId: string;
  projectKey: string;
}

// TODO: every change dispatches through useFeaturesActions(), never a mutation hook or a direct core write.
// TODO: dates come from the server's labels (statusVerified.atLabel), never formatted on the client.
// TODO: show the D8 warning on closing with open stories.
// TODO: 44 pt tap targets and 15 pt body.
// TODO: layout and copy from design/mobileapp/feature-board.html?state=feature.
export function FeatureDetails({ row }: FeatureDetailsProps) {
  return <View testID={featureDetailsId(row.key)} />;
}
