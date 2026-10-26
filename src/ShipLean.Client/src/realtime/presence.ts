// The ids on a screen other than mine. Until I am known nobody is listed: I must never show up in my own stack.
export function withoutMe(userIds: readonly string[], meId: string | undefined): string[] {
  return meId === undefined ? [] : userIds.filter((id) => id !== meId);
}
