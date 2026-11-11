package app.shiplean.modelfiles

import expo.modules.kotlin.modules.Module
import expo.modules.kotlin.modules.ModuleDefinition

class ModelFilesModule : Module() {
  override fun definition() = ModuleDefinition {
    Name("ShipLeanModelFiles")

    // Android has no per-file backup flag. The app turns backup off (android.allowBackup false in app.json), so the
    // model files are never backed up and this is a no-op.
    AsyncFunction("excludeFromBackup") { _: String -> }

    // The download's foreground service (ModelDownloadService): started or updated with the percentage, stopped when
    // the download stops. False when Android refused the start.
    Function("startDownloadService") { percent: Int ->
      val context = appContext.reactContext?.applicationContext ?: return@Function false
      ModelDownloadService.startOrUpdate(context, percent)
    }

    Function("stopDownloadService") {
      val context = appContext.reactContext?.applicationContext ?: return@Function
      ModelDownloadService.stop(context)
    }
  }
}
