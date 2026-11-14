// The attachment page's ids, one block per slice.
export const attachmentTestIds = {
  // S0c page
  screen: 'attachment-screen',

  // A5 attachment page
  back: 'attachment-back',
  workItem: 'attachment-work-item',
  name: 'attachment-name',
  meta: 'attachment-meta',
  rendered: 'attachment-rendered',
  source: 'attachment-source',
  download: 'attachment-download',
  replace: 'attachment-replace',
  replaceInput: 'attachment-replace-input',
  body: 'attachment-body',
  sourceText: 'attachment-source-text',
  outline: 'attachment-outline',
  outlineEntry: 'attachment-outline-entry',
  images: 'attachment-images',
  imageRow: 'attachment-image-row',
  imageStatus: 'attachment-image-status',
  siblings: 'attachment-siblings',
  sibling: 'attachment-sibling',

  // SHIP-715 touch
  header: 'attachment-header',
  nameButton: 'attachment-name-button',
  facts: 'attachment-facts',
  contentsPill: 'attachment-contents-pill',
  contentsEntry: 'attachment-contents-entry',
  notFound: 'attachment-not-found',
  loadFailed: 'attachment-load-failed',
  loadFailedDownload: 'attachment-load-failed-download',
  noHistory: 'attachment-no-history',

  // SHIP-715 image viewer
  viewerCounter: 'attachment-viewer-counter',
  viewerStage: 'attachment-viewer-stage',
  viewerPrev: 'attachment-viewer-prev',
  viewerNext: 'attachment-viewer-next',
  viewerThumbs: 'attachment-viewer-thumbs',
  viewerThumb: 'attachment-viewer-thumb',
  replaceError: 'attachment-replace-error',

  // SHIP-198 edit
  edit: 'attachment-edit',
  editor: 'attachment-editor',
  editorInput: 'attachment-editor-input',
  editorPasting: 'attachment-editor-pasting',
  editorPasteError: 'attachment-editor-paste-error',
  // SHIP-715 phone edit: the top bar's pinned spot, under the editor's own toolbar
  editorToolbarSlot: 'attachment-editor-toolbar-slot',
  save: 'attachment-save',
  cancel: 'attachment-cancel',
  saveError: 'attachment-save-error',
} as const;
