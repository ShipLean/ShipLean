import { platform } from '@shiplean/core/platform';
import { useRouter } from 'expo-router';
import { useEffect, useId, useState, useSyncExternalStore, type FunctionComponent } from 'react';
import { Pressable, Text, View } from 'react-native';
import { links } from '../links';
import { bannerView, memoryView } from '../setup/bannerView';
import { rateOf, timeLeft, withSample, type Sample } from '../setup/downloadView';
import { SETUP_OFFERED_KEY } from '../setup/firstRun';
import { MemorySheet } from '../setup/MemorySheet';
import { MobileDataSheet } from '../setup/MobileDataSheet';
import {
  claimMobileDataAsk,
  endMobileDataAsk,
  mobileDataAskOwner,
  releaseMobileDataAsk,
  subscribeMobileDataAsk,
} from '../setup/mobileDataAsk';
import { ProgressLine } from '../setup/SetupParts';
import { useModelStatus } from '../setup/useModelStatus';
import { useTheme } from '../theme';
import { Banner } from '../ui/Banner';
import { tint } from '../ui/layout';
import { downloadBannerIds } from './DownloadBanner.testIds';
import { engines } from './engines';

export interface DownloadBannerProps {
  placement: 'list' | 'detail';
}

// A download that has ended: the next pause for Wi-Fi is a new download, so it may ask again.
const ENDED = ['ready', 'none', 'skipped', 'failed'];

// While the models download, list and detail screens say so, and a tap opens the Downloading page. A pause for
// Wi-Fi asks once whether to use mobile data. Skipped voice offers Set up, and a memory failure its sheet. The root
// stays mounted and empty when there is nothing to say.
export const DownloadBanner: FunctionComponent<DownloadBannerProps> = () => {
  const theme = useTheme();
  const router = useRouter();
  const status = useModelStatus();
  const [samples, setSamples] = useState<Sample[]>([]);
  const owner = useId();
  const askOpen = useSyncExternalStore(
    subscribeMobileDataAsk,
    () => mobileDataAskOwner() === owner,
  );
  const [memoryOpen, setMemoryOpen] = useState(false);

  // The rate comes from the progress seen while the app is open.
  useEffect(() => {
    const store = engines().models;
    return store.subscribe(() => {
      const now = store.status();
      setSamples((prev) =>
        now.phase === 'downloading'
          ? withSample(prev, { at: Date.now(), bytes: now.bytesDone })
          : [],
      );
    });
  }, []);

  // Paused for Wi-Fi means off Wi-Fi with a connection: ask once per download.
  useEffect(() => {
    const kv = platform().persistentStore();
    if (status.phase === 'paused' && status.reason === 'wifi') claimMobileDataAsk(kv, owner);
    else if (ENDED.includes(status.phase)) endMobileDataAsk(kv);
  }, [status.phase, status.reason, owner]);
  useEffect(() => () => releaseMobileDataAsk(owner), [owner]);

  const left = timeLeft(Math.max(0, status.bytesTotal - status.bytesDone), rateOf(samples));
  const view = bannerView(status, {
    setupOffered: platform().persistentStore().getItem(SETUP_OFFERED_KEY) !== null,
    left,
  });

  return (
    <View testID={downloadBannerIds.root}>
      {view === null ? null : view.kind === 'gettingReady' || view.kind === 'paused' ? (
        <Pressable
          testID={downloadBannerIds.progress}
          accessibilityRole="button"
          onPress={() => router.push(links.setup('downloading'))}
          style={{
            gap: theme.space[2],
            marginTop: theme.space[3],
            marginHorizontal: theme.space[4],
            paddingVertical: theme.space[2],
            paddingHorizontal: theme.space[3],
            minHeight: theme.touch.control,
            borderRadius: theme.radius.lg,
            backgroundColor: tint(theme.colors.info, 0.14),
          }}
        >
          <Text
            testID={downloadBannerIds.progressText}
            style={{ color: theme.colors.text, fontSize: theme.font.size.base }}
          >
            {view.text}
          </Text>
          <ProgressLine testID={downloadBannerIds.progressBar} fraction={view.fraction} />
        </Pressable>
      ) : view.kind === 'off' ? (
        <Banner
          testID={downloadBannerIds.off}
          tone="info"
          text={view.text}
          action={{ label: view.action, onPress: () => router.push(links.setup('models')) }}
        />
      ) : (
        <Banner
          testID={downloadBannerIds.memory}
          tone="warn"
          text={view.text}
          action={{ label: view.action, onPress: () => setMemoryOpen(true) }}
        />
      )}
      <MobileDataSheet
        open={askOpen}
        leftBytes={Math.max(0, status.bytesTotal - status.bytesDone)}
        onClose={() => releaseMobileDataAsk(owner)}
      />
      {status.phase === 'failed' && status.reason === 'memory' ? (
        <MemorySheet
          open={memoryOpen}
          view={memoryView(status)}
          onClose={() => setMemoryOpen(false)}
        />
      ) : null}
    </View>
  );
};
