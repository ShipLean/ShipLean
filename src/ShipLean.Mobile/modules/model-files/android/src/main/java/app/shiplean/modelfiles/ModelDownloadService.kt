package app.shiplean.modelfiles

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.Context
import android.content.Intent
import android.content.pm.ServiceInfo
import android.os.IBinder

// Keeps the app's process in the foreground while the voice models download, so Android doesn't freeze or kill it
// when the person leaves the app. The download itself is expo-file-system's, in the same process; this only holds the
// process up and shows the ongoing notification Android requires for that. Type dataSync (FOREGROUND_SERVICE_DATA_SYNC
// in app.json). Android 15 limits dataSync to 6 hours a day: onTimeout stops it and the download carries on while the
// app is open, as it would without the service.
class ModelDownloadService : Service() {
  override fun onBind(intent: Intent?): IBinder? = null

  override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
    val percent = intent?.getIntExtra(EXTRA_PERCENT, -1) ?: -1
    startForeground(NOTIFICATION_ID, notification(this, percent), ServiceInfo.FOREGROUND_SERVICE_TYPE_DATA_SYNC)
    running = true
    return START_NOT_STICKY
  }

  override fun onTimeout(startId: Int, fgsType: Int) {
    stopSelf()
  }

  override fun onDestroy() {
    running = false
    super.onDestroy()
  }

  companion object {
    private const val CHANNEL_ID = "voice-model-download"
    private const val NOTIFICATION_ID = 7301
    private const val EXTRA_PERCENT = "percent"

    @Volatile
    var running = false
      private set

    // Starts the service, or updates its notification when it is already up (a second start from the background
    // isn't allowed, a notification update is). False when Android refuses the start: the app isn't in the
    // foreground, as when a retry fires after the person left.
    fun startOrUpdate(context: Context, percent: Int): Boolean {
      if (running) {
        context.getSystemService(NotificationManager::class.java)?.notify(NOTIFICATION_ID, notification(context, percent))
        return true
      }
      return try {
        context.startForegroundService(Intent(context, ModelDownloadService::class.java).putExtra(EXTRA_PERCENT, percent))
        true
      } catch (e: IllegalStateException) {
        // ForegroundServiceStartNotAllowedException (Android 12+) is an IllegalStateException.
        false
      } catch (e: SecurityException) {
        false
      }
    }

    fun stop(context: Context) {
      context.stopService(Intent(context, ModelDownloadService::class.java))
    }

    private fun notification(context: Context, percent: Int): Notification {
      val manager = context.getSystemService(NotificationManager::class.java)
      if (manager?.getNotificationChannel(CHANNEL_ID) == null) {
        manager?.createNotificationChannel(
          NotificationChannel(CHANNEL_ID, "Voice model download", NotificationManager.IMPORTANCE_LOW),
        )
      }
      val open = context.packageManager.getLaunchIntentForPackage(context.packageName)
      val builder = Notification.Builder(context, CHANNEL_ID)
        .setSmallIcon(android.R.drawable.stat_sys_download)
        .setContentTitle("Downloading voice models")
        .setOngoing(true)
        .setOnlyAlertOnce(true)
        .setForegroundServiceBehavior(Notification.FOREGROUND_SERVICE_IMMEDIATE)
        .setProgress(100, percent.coerceIn(0, 100), percent < 0)
      if (percent >= 0) builder.setContentText("$percent%")
      if (open != null) {
        builder.setContentIntent(
          PendingIntent.getActivity(context, 0, open, PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT),
        )
      }
      return builder.build()
    }
  }
}
