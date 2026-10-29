import { createContext, useContext } from 'react';

/** A story's Feature, shown before the story on a People card. */
export interface CardFeature {
  key: string;
  title: string;
}

/**
 * Story id → its Feature, provided by the Feature board when its tree has more than one Feature. Without a provider (the
 * Taskboard, a leaf Feature) cards show the story alone and People rows show no Feature count.
 */
export const CardFeatureContext = createContext<ReadonlyMap<string, CardFeature> | null>(null);

export const useCardFeatures = () => useContext(CardFeatureContext);
