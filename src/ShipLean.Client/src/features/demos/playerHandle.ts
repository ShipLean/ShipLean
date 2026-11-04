import type { DemoPlayerHandle } from './DemoPlayerHandle';

// What a DemoPlayer binds to a handle: the file's element, or the embed's start setter.
const videos = new WeakMap<DemoPlayerHandle, HTMLVideoElement>();
const embeds = new WeakMap<DemoPlayerHandle, (seconds: number) => void>();
const listeners = new WeakMap<DemoPlayerHandle, Set<(seconds: number) => void>>();

/** A handle whose `seekTo` does nothing until a DemoPlayer binds its video or embed to it. */
export function newPlayerHandle(): DemoPlayerHandle {
  const handle: DemoPlayerHandle = {
    seekTo(seconds) {
      const video = videos.get(handle);
      if (video !== undefined) {
        video.currentTime = seconds;
        void video.play();
      } else {
        embeds.get(handle)?.(seconds);
      }
      reportTime(handle, seconds);
    },
  };
  listeners.set(handle, new Set());
  return handle;
}

/** DemoPlayer's end: the video the handle drives, or null when it goes. */
export function bindVideo(handle: DemoPlayerHandle, video: HTMLVideoElement | null) {
  if (video === null) videos.delete(handle);
  else videos.set(handle, video);
}

/** DemoPlayer's end for a YouTube embed: what to do with a whole-second start. */
export function bindEmbed(handle: DemoPlayerHandle, seek: ((seconds: number) => void) | null) {
  if (seek === null) embeds.delete(handle);
  else embeds.set(handle, seek);
}

/** DemoPlayer's end: tell the chapters where the player is. */
export function reportTime(handle: DemoPlayerHandle, seconds: number) {
  listeners.get(handle)?.forEach((listener) => listener(seconds));
}

/** DemoChapters' end: hear where the player is (a seek, or the file playing). Returns the way to stop listening. */
export function subscribeTime(handle: DemoPlayerHandle, listener: (seconds: number) => void) {
  const heard = listeners.get(handle);
  heard?.add(listener);
  return () => void heard?.delete(listener);
}
