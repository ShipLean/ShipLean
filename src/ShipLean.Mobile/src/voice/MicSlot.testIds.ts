export type MicPlacement = 'tabBar' | 'dock';

export const micSlotId = (placement: MicPlacement) => `mic-${placement}`;
export const micLabelId = (placement: MicPlacement) => `mic-${placement}-label`;
export const micStrikeId = (placement: MicPlacement) => `mic-${placement}-strike`;
