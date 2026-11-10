import { fireEvent, screen } from '@testing-library/react-native';
import { openBrowserAsync } from 'expo-web-browser';
import { renderScreen } from '../test-support/renderScreen';
import { MODEL_FILES } from '../voice/manifest';
import { ModelLicences } from './ModelLicences';
import { licenceEntryId, licenceLinkId } from './ModelLicences.testIds';

// Links are '' until they are pinned: entries with links, Parakeet split over two files as it may be.
jest.mock('../voice/manifest', () => {
  const actual = jest.requireActual<typeof import('../voice/manifest')>('../voice/manifest');
  return {
    ...actual,
    MODEL_FILES: actual.MODEL_FILES.flatMap((f) =>
      f.model === 'parakeet'
        ? [
            { ...f, id: `${f.id}-a`, link: `https://models.test/${f.model}` },
            { ...f, id: `${f.id}-b`, link: `https://models.test/${f.model}` },
          ]
        : [{ ...f, link: `https://models.test/${f.model}` }],
    ),
  };
});

describe('ModelLicences', () => {
  it('lists every manifest file with its licence and link', async () => {
    await renderScreen(<ModelLicences />);

    const models = [...new Set(MODEL_FILES.map((f) => f.model))];
    expect(models).toHaveLength(3);
    for (const model of models) {
      const file = MODEL_FILES.find((f) => f.model === model)!;
      const entry = screen.getByTestId(licenceEntryId(model));
      expect(entry).toHaveTextContent(file.name, { exact: false });
      expect(entry).toHaveTextContent(file.author, { exact: false });
      expect(entry).toHaveTextContent(file.changes, { exact: false });
      expect(entry).toHaveTextContent(
        file.licence === 'Apache-2.0' ? 'Licence: Apache 2.0' : `Licence: ${file.licence}`,
        { exact: false },
      );
      expect(screen.getByTestId(licenceLinkId(model))).toHaveTextContent(file.link, {
        exact: false,
      });
    }
    // A model split over two files is still one entry.
    expect(screen.getAllByTestId(/^settings-licences-parakeet$/)).toHaveLength(1);
  });

  it('a link opens in the browser', async () => {
    await renderScreen(<ModelLicences />);

    await fireEvent.press(screen.getByTestId(licenceLinkId('qwen3-4b')));

    expect(openBrowserAsync).toHaveBeenCalledWith('https://models.test/qwen3-4b');
  });
});
