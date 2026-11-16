Check `ffmpeg`, Playwright and Python 3.10 or later. Install what is missing under `~/.local/demo-tools`:

```bash
mkdir -p ~/.local/demo-tools && cd ~/.local/demo-tools
command -v uv >/dev/null || export PATH="$HOME/.local/bin:$PATH"
if command -v uv >/dev/null; then
  uv venv venv && uv pip install --python venv/bin/python kokoro-onnx soundfile imageio-ffmpeg playwright
else
  rm -rf venv && python3 -m venv venv && venv/bin/pip install kokoro-onnx soundfile imageio-ffmpeg playwright
fi
venv/bin/playwright install chromium
B=https://github.com/thewh1teagle/kokoro-onnx/releases/download/model-files-v1.0
curl -sLO $B/kokoro-v1.0.int8.onnx && curl -sLO $B/voices-v1.0.bin
FF=$(venv/bin/python -c 'import imageio_ffmpeg;print(imageio_ffmpeg.get_ffmpeg_exe())')
```

`$FF` has libx264, aac, loudnorm and overlay, and **no ffprobe**. Read an audio duration with `soundfile` (`soundfile.info(path).duration`) and a video duration by parsing the `Duration:` line of `$FF -hide_banner -i file.mp4 2>&1` (the command exits non-zero without an output; that is fine). The commands above install Playwright for Python and its Chromium; another Playwright you already have works as well. `$FF` is a shell variable: set it again in every new shell. If neither `uv` nor `python3 -m venv` works, that is a tool you can't have: comment and stop.

If a tool can't be had, `comment` on `{{demo_item}}` with the exact failure and stop.
