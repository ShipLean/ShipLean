import { nativeInputs, onInputsChanged, type NativeInput } from '../../../modules/audio-input';
import { NativeAudioInputs } from './audioInputs';

jest.mock('../../../modules/audio-input', () => ({
  nativeInputs: jest.fn(),
  onInputsChanged: jest.fn(),
}));

const builtIn: NativeInput = { id: '7', kind: 'phone', name: 'Built-in', connected: true };
const pods: NativeInput = { id: '12', kind: 'bluetooth', name: 'Pods', connected: true };
const cable: NativeInput = { id: '15', kind: 'wired', name: 'Headset', connected: true };

function listed(...inputs: NativeInput[]) {
  jest.mocked(nativeInputs).mockReturnValue(inputs);
}

describe('NativeAudioInputs', () => {
  it('lists the phone first and always connected, even when the OS lists it last', () => {
    listed(cable, pods, builtIn);

    const ids = new NativeAudioInputs().list();

    expect(ids[0]).toEqual({ id: 'phone', kind: 'phone', name: 'Phone', connected: true });
    expect(ids.map((i) => i.kind)).toEqual(['phone', 'bluetooth', 'wired']);
  });

  it('lists the phone when the OS lists nothing', () => {
    listed();

    expect(new NativeAudioInputs().list()).toEqual([
      { id: 'phone', kind: 'phone', name: 'Phone', connected: true },
    ]);
  });

  it('maps Bluetooth and wired inputs by the OS id, name and kind', () => {
    listed(builtIn, pods, cable);

    expect(new NativeAudioInputs().list().slice(1)).toEqual([
      { id: '12', kind: 'bluetooth', name: 'Pods', connected: true },
      { id: '15', kind: 'wired', name: 'Headset', connected: true },
    ]);
  });

  it('tells subscribers when the OS reports a change, and stops after unsubscribe', () => {
    let fire = () => {};
    const remove = jest.fn();
    jest.mocked(onInputsChanged).mockImplementation((cb) => {
      fire = cb;
      return remove;
    });
    const seen = jest.fn();

    const unsubscribe = new NativeAudioInputs().subscribe(seen);
    fire();
    unsubscribe();

    expect(seen).toHaveBeenCalledTimes(1);
    expect(remove).toHaveBeenCalledTimes(1);
  });
});
