import { FeaturesActionsProvider } from '../../features/FeaturesActions';
import type { ProjectSlotProps } from './types';

export function FeaturesProvider({ children }: ProjectSlotProps) {
  return <FeaturesActionsProvider>{children}</FeaturesActionsProvider>;
}
