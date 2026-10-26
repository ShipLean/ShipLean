import { ConnectedAgentsPanel } from '../features/agents/connected/ConnectedAgentsPanel';

export type ProfileConnectedAgentsSlotProps = Record<string, never>;

export function ProfileConnectedAgentsSlot(props: ProfileConnectedAgentsSlotProps) {
  void props;
  return <ConnectedAgentsPanel />;
}
