import { fireEvent, screen } from '@testing-library/react-native';
import { renderScreen } from '../../test-support/renderScreen';
import { installFakeEngines } from '../../test-support/voiceUi';
import { MicCheck } from './MicCheck';
import {
  micCheckDoneId,
  micCheckInputId,
  micCheckLevelId,
  micCheckLevelLabelId,
  micCheckTypeInsteadId,
} from './MicCheck.testIds';

describe('MicCheck', () => {
  it('shows the live level and the input, and Type instead calls typeInstead', async () => {
    const engines = installFakeEngines();
    engines.recognizer.setInputName('AirPods Pro');
    engines.recognizer.script([], '', [0.02, 0.5]);
    const onTypeInstead = jest.fn();
    const onClose = jest.fn();
    await renderScreen(<MicCheck onTypeInstead={onTypeInstead} onClose={onClose} />);

    expect(engines.recognizer.starts).toHaveLength(1);
    expect(screen.getByTestId(micCheckLevelId).props.accessibilityValue).toEqual({
      min: 0,
      max: 100,
      now: 50,
    });
    expect(screen.getByTestId(micCheckLevelLabelId)).toHaveTextContent('Hearing you');
    expect(screen.getByTestId(micCheckInputId)).toHaveTextContent(/AirPods Pro/);
    expect(screen.queryByText('Use iPhone mic')).toBeNull();

    await fireEvent.press(screen.getByTestId(micCheckTypeInsteadId));
    expect(onTypeInstead).toHaveBeenCalledTimes(1);
    expect(onClose).not.toHaveBeenCalled();
    await fireEvent.press(screen.getByTestId(micCheckDoneId));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('reads Quiet when nothing comes in, and says so when the phone names no input', async () => {
    const engines = installFakeEngines();
    engines.recognizer.script([], '', [0]);
    await renderScreen(<MicCheck onTypeInstead={() => {}} onClose={() => {}} />);

    expect(screen.getByTestId(micCheckLevelLabelId)).toHaveTextContent('Quiet');
    expect(screen.getByTestId(micCheckLevelId).props.accessibilityValue.now).toBe(0);
    expect(screen.getByTestId(micCheckInputId)).toHaveTextContent(
      "The phone didn't say which microphone it's using.",
    );
  });

  it('cancels its own session when it closes', async () => {
    const engines = installFakeEngines();
    const view = await renderScreen(<MicCheck onTypeInstead={() => {}} onClose={() => {}} />);
    expect(engines.recognizer.cancelled).toBe(0);
    await view.unmount();
    expect(engines.recognizer.cancelled).toBe(1);
  });
});
