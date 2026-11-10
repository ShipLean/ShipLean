package app.shiplean.modelfiles

import expo.modules.kotlin.modules.Module
import expo.modules.kotlin.modules.ModuleDefinition

// Android has no per-file backup flag. The app turns backup off (android.allowBackup false in app.json), so the
// model files are never backed up and this is a no-op.
class ModelFilesModule : Module() {
  override fun definition() = ModuleDefinition {
    Name("ShipLeanModelFiles")

    AsyncFunction("excludeFromBackup") { _: String -> }
  }
}
