import type { DemoPlayerHandle } from './DemoPlayerHandle';

const videos = new WeakMap<DemoPlayerHandle, HTMLVideoElement>();

/** A handle whose `seekTo` does nothing until a DemoPlayer binds its video to it. */
export function newPlayerHandle(): DemoPlayerHandle {
  const handle: DemoPlayerHandle = {
    seekTo(seconds) {
      const video = videos.get(handle);
      if (video !== undefined) video.currentTime = seconds;
    },
  };
  return handle;
}

/** DemoPlayer's end: the video the handle drives, or null when it goes. */
export function bindVideo(handle: DemoPlayerHandle, video: HTMLVideoElement | null) {
  if (video === null) videos.delete(handle);
  else videos.set(handle, video);
}
