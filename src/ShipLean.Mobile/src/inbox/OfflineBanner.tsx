export interface OfflineBannerProps {
  // The server's clock time of the Inbox as last loaded (InboxDto.loadedAtLabel).
  loadedAtLabel: string;
}

// Root id inboxIds.offlineBanner.
// TODO: the offline banner (the Inbox as last loaded, and when) replaces this stub, which renders nothing.
export function OfflineBanner({ loadedAtLabel }: OfflineBannerProps) {
  void loadedAtLabel;
  return null;
}
