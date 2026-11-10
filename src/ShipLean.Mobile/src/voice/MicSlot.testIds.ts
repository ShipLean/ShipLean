export type MicPlacement = 'tabBar' | 'dock';

export const micSlotId = (placement: MicPlacement) => `mic-${placement}`;
