// react-native-sherpa-onnx lists two helper libraries as peers. Its /stt entry, the only one the app imports, loads
// @dr.pogodin/react-native-fs when it loads (stt → utils → download/validation), so that one is a direct dependency
// and autolinks: without its native module the app crashes at launch. The background downloader is only reached from
// the helpers the app never imports, so keep its native code out of autolinking.
module.exports = {
  dependencies: {
    '@kesha-antonov/react-native-background-downloader': {
      platforms: { android: null, ios: null },
    },
  },
};
