import { screen } from '@testing-library/react-native';
import { renderScreen } from '../test-support/renderScreen';
import { DownloadBanner } from '../voice/DownloadBanner';
import { downloadBannerIds } from '../voice/DownloadBanner.testIds';
import { FirstResultSheet } from './FirstResultSheet';
import { firstResultSheetIds } from './FirstResultSheet.testIds';
import { FromSetupTip } from './FromSetupTip';
import { fromSetupTipIds } from './FromSetupTip.testIds';
import { MicrophoneStep } from './MicrophoneStep';
import { microphoneStepIds } from './MicrophoneStep.testIds';
import { ModelLicences } from './ModelLicences';
import { modelLicencesIds } from './ModelLicences.testIds';
import { ModelsStep } from './ModelsStep';
import { modelsStepIds } from './ModelsStep.testIds';
import { DownloadingStep } from './DownloadingStep';
import { downloadingStepIds } from './DownloadingStep.testIds';
import { ReadyCoachMark } from './ReadyCoachMark';
import { readyCoachMarkIds } from './ReadyCoachMark.testIds';
import { VoiceSettings } from './VoiceSettings';
import { voiceSettingsIds } from './VoiceSettings.testIds';

const noop = () => undefined;

describe('setup seams', () => {
  it.each([
    [
      'ModelsStep',
      'setup-models',
      modelsStepIds.root,
      <ModelsStep onContinue={noop} onSkip={noop} />,
    ],
    [
      'MicrophoneStep',
      'setup-microphone',
      microphoneStepIds.root,
      <MicrophoneStep onContinue={noop} onNotNow={noop} />,
    ],
    [
      'DownloadingStep',
      'setup-downloading',
      downloadingStepIds.root,
      <DownloadingStep onUseApp={noop} />,
    ],
    ['VoiceSettings', 'settings-voice', voiceSettingsIds.root, <VoiceSettings />],
    ['ModelLicences', 'settings-licences', modelLicencesIds.root, <ModelLicences />],
    [
      'ReadyCoachMark',
      'voice-ready-coach',
      readyCoachMarkIds.root,
      <ReadyCoachMark onTry={noop} onSkip={noop} />,
    ],
    [
      'FirstResultSheet',
      'sheet-first-result',
      firstResultSheetIds.root,
      <FirstResultSheet open projectId="p1" projectKey="SHIP" onOpenInbox={noop} onClose={noop} />,
    ],
    ['FromSetupTip', 'inbox-from-setup', fromSetupTipIds.root, <FromSetupTip projectKey="SHIP" />],
  ])('each setup stub renders its root id from its props: %s', async (_name, id, exported, ui) => {
    await renderScreen(ui);

    expect(exported).toBe(id);
    expect(screen.getByTestId(id)).toBeOnTheScreen();
  });

  it('DownloadBanner renders its root id for both placements', async () => {
    for (const placement of ['list', 'detail'] as const) {
      const view = await renderScreen(<DownloadBanner placement={placement} />);

      expect(screen.getByTestId(downloadBannerIds.root)).toBeOnTheScreen();
      expect(downloadBannerIds.root).toBe('voice-download-banner');
      view.unmount();
    }
  });
});
