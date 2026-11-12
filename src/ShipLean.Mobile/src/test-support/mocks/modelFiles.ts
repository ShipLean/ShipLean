// The jest mock of the local model-files module (modules/model-files): no native side in jest. Specs assert the calls
// with jest.mocked(excludeFromBackup) and the download service's with jest.mocked(startDownloadService).
jest.mock('../../../modules/model-files', () => ({
  __esModule: true,
  excludeFromBackup: jest.fn(async () => undefined),
  startDownloadService: jest.fn(() => true),
  stopDownloadService: jest.fn(),
}));

export {};
