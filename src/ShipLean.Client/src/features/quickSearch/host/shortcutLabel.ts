interface NavigatorWithPlatform {
  userAgentData?: { platform?: string };
  platform?: string;
}

// The quick search shortcut as the platform writes it.
export function shortcutLabel(nav: NavigatorWithPlatform = navigator): string {
  const platform = nav.userAgentData?.platform ?? nav.platform ?? '';
  return /mac/i.test(platform) ? '⌘P' : 'Ctrl+P';
}
