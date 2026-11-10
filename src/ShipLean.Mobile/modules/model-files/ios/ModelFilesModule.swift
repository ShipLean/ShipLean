import ExpoModulesCore

// Keeps the voice model files out of iCloud backup: they are large and can be downloaded again.
public class ModelFilesModule: Module {
  public func definition() -> ModuleDefinition {
    Name("ShipLeanModelFiles")

    // Sets NSURLIsExcludedFromBackupKey on a file or a directory (a directory's contents go with it).
    AsyncFunction("excludeFromBackup") { (url: URL) in
      var target = url
      var values = URLResourceValues()
      values.isExcludedFromBackup = true
      try target.setResourceValues(values)
    }
  }
}
