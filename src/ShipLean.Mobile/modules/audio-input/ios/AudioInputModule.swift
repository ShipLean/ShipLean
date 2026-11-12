import AVFoundation
import ExpoModulesCore

// The inputs this phone can record from. An input's id is the port's uid, the same id the patched expo-audio stream
// takes as preferredInputId and reports as routedInputId.
public class AudioInputModule: Module {
  private var observer: NSObjectProtocol?

  public func definition() -> ModuleDefinition {
    Name("ShipLeanAudioInput")

    Events("onInputsChanged")

    Function("list") { () -> [[String: Any]] in
      let inputs = AVAudioSession.sharedInstance().availableInputs ?? []
      return inputs.compactMap { port -> [String: Any]? in
        guard let kind = Self.kind(of: port.portType) else { return nil }
        return [
          "id": port.uid,
          "kind": kind,
          "name": kind == "phone" ? "Phone" : port.portName,
          "connected": true
        ]
      }
    }

    OnCreate {
      self.observer = NotificationCenter.default.addObserver(
        forName: AVAudioSession.routeChangeNotification,
        object: nil,
        queue: .main
      ) { [weak self] _ in
        self?.sendEvent("onInputsChanged")
      }
    }

    OnDestroy {
      if let observer = self.observer {
        NotificationCenter.default.removeObserver(observer)
      }
      self.observer = nil
    }
  }

  private static func kind(of type: AVAudioSession.Port) -> String? {
    switch type {
    case .builtInMic: return "phone"
    case .bluetoothHFP, .bluetoothLE: return "bluetooth"
    case .headsetMic, .usbAudio: return "wired"
    default: return nil
    }
  }
}
