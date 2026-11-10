export const downloadBannerIds = {
  root: 'voice-download-banner',
  // Downloading, checking or paused: the whole banner is the tap target for the Downloading page.
  progress: 'voice-download-banner-progress',
  progressText: 'voice-download-banner-progress-text',
  progressBar: 'voice-download-banner-progress-bar',
  // Voice is off (Set up) and the memory failure (See options): the shared Banner's id, its action is `-action`.
  off: 'voice-download-banner-off',
  memory: 'voice-download-banner-memory',
} as const;
