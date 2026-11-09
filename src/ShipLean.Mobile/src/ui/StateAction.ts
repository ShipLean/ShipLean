// The one button an empty or error state may offer.
export interface StateAction {
  label: string;
  onPress: () => void;
  // The action is running (a Retry that is fetching): shown busy and not pressable.
  busy?: boolean;
}
