// react-native-sherpa-onnx lists these two as optional peers and pnpm installs them. The app never imports the helpers
// that use them (only react-native-sherpa-onnx/stt), so keep their native code out of autolinking.
module.exports = {
  dependencies: {
    '@dr.pogodin/react-native-fs': { platforms: { android: null, ios: null } },
    '@kesha-antonov/react-native-background-downloader': {
      platforms: { android: null, ios: null },
    },
  },
};
