export const micPermissionSheetId = 'sheet-mic-permission';

export const micPermissionIds = {
  // The sheet's three forms: ask (sheet-ask), ios (sheet-ios) and android (sheet-android).
  formAsk: 'mic-permission-ask',
  formIos: 'mic-permission-ios',
  formAndroid: 'mic-permission-android',
  lead: 'mic-permission-lead',
  promises: 'mic-permission-promises',
  allow: 'mic-permission-allow',
  openSettings: 'mic-permission-open-settings',
  appSettings: 'mic-permission-app-settings',
  typeInstead: 'mic-permission-type-instead',
  note: 'mic-permission-note',
  // The small drawings of the phone's own rows, and the one row to change.
  settingsDrawing: 'mic-permission-settings-drawing',
  appInfoDrawing: 'mic-permission-app-info-drawing',
  markedRow: 'mic-permission-marked-row',
} as const;
