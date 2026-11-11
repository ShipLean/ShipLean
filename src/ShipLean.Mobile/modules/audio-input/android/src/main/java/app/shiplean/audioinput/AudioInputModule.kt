package app.shiplean.audioinput

import android.content.Context
import android.media.AudioDeviceCallback
import android.media.AudioDeviceInfo
import android.media.AudioManager
import android.os.Handler
import android.os.Looper
import expo.modules.kotlin.modules.Module
import expo.modules.kotlin.modules.ModuleDefinition

// The inputs this phone can record from. An input's id is a stable key, "<kind>:<address>" (the Bluetooth MAC, API 28+)
// or "<kind>:<productName>" when the address is empty, never AudioDeviceInfo.id, which changes on every reconnect. It
// is the same key the patched expo-audio stream takes as preferredInputId and reports as routedInputId.
class AudioInputModule : Module() {
  private var callback: AudioDeviceCallback? = null

  private val audioManager: AudioManager?
    get() = appContext.reactContext?.getSystemService(Context.AUDIO_SERVICE) as? AudioManager

  override fun definition() = ModuleDefinition {
    Name("ShipLeanAudioInput")

    Events("onInputsChanged")

    Function("list") {
      val manager = audioManager ?: return@Function emptyList<Map<String, Any>>()
      manager.getDevices(AudioManager.GET_DEVICES_INPUTS).mapNotNull { device ->
        val kind = kindOf(device) ?: return@mapNotNull null
        mapOf(
          "id" to stableKey(device, kind),
          "kind" to kind,
          "name" to nameOf(device, kind),
          "connected" to true
        )
      }
    }

    OnCreate {
      val deviceCallback = object : AudioDeviceCallback() {
        override fun onAudioDevicesAdded(addedDevices: Array<out AudioDeviceInfo>?) {
          sendEvent("onInputsChanged")
        }

        override fun onAudioDevicesRemoved(removedDevices: Array<out AudioDeviceInfo>?) {
          sendEvent("onInputsChanged")
        }
      }
      callback = deviceCallback
      audioManager?.registerAudioDeviceCallback(deviceCallback, Handler(Looper.getMainLooper()))
    }

    OnDestroy {
      callback?.let { audioManager?.unregisterAudioDeviceCallback(it) }
      callback = null
    }
  }

  private fun stableKey(device: AudioDeviceInfo, kind: String): String {
    val address = if (android.os.Build.VERSION.SDK_INT >= 28) device.address.orEmpty() else ""
    val suffix = address.ifEmpty { device.productName?.toString().orEmpty() }
    return "$kind:$suffix"
  }

  private fun kindOf(device: AudioDeviceInfo): String? = when (device.type) {
    AudioDeviceInfo.TYPE_BUILTIN_MIC -> "phone"
    AudioDeviceInfo.TYPE_BLUETOOTH_SCO, AudioDeviceInfo.TYPE_BLE_HEADSET -> "bluetooth"
    AudioDeviceInfo.TYPE_WIRED_HEADSET, AudioDeviceInfo.TYPE_USB_HEADSET -> "wired"
    else -> null
  }

  private fun nameOf(device: AudioDeviceInfo, kind: String): String {
    val product = device.productName?.toString()?.trim().orEmpty()
    return when {
      kind == "phone" -> "Phone"
      product.isNotEmpty() -> product
      kind == "bluetooth" -> "Bluetooth headset"
      else -> "Wired headset"
    }
  }
}
