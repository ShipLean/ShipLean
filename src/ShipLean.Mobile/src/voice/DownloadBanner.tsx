import type { FunctionComponent } from 'react';
import { View } from 'react-native';
import { downloadBannerIds } from './DownloadBanner.testIds';

export interface DownloadBannerProps {
  placement: 'list' | 'detail';
}

// TODO: replace this empty stub with the model download banner.
export const DownloadBanner: FunctionComponent<DownloadBannerProps> = () => (
  <View testID={downloadBannerIds.root} />
);
