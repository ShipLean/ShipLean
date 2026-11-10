import { act, renderHook } from '@testing-library/react-native';
import '../../test-support/voiceUi';
import { requestComposer, useComposerRequest, type ComposerRequest } from './composerRequest';

const typeInstead: ComposerRequest = {
  mode: 'command',
  text: '',
  reason: 'micOff',
  questionNumber: null,
};
const edit: ComposerRequest = {
  mode: 'command',
  text: 'set this to active',
  reason: 'edit',
  questionNumber: null,
};

describe('composerRequest', () => {
  it('a request is read once, then consumed', async () => {
    const { result } = await renderHook(() => useComposerRequest());
    expect(result.current.request).toBeNull();

    await act(() => requestComposer(typeInstead));
    expect(result.current.request).toEqual(typeInstead);

    await act(() => result.current.consume());
    expect(result.current.request).toBeNull();

    // A second reader mounted after the consume sees nothing pending.
    const later = await renderHook(() => useComposerRequest());
    expect(later.result.current.request).toBeNull();
  });

  it('a new request replaces the pending one', async () => {
    const { result } = await renderHook(() => useComposerRequest());

    await act(() => requestComposer(typeInstead));
    await act(() => requestComposer(edit));
    expect(result.current.request).toEqual(edit);

    await act(() => result.current.consume());
    expect(result.current.request).toBeNull();
  });
});
