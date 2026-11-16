The tools are installed on this machine: `demo-python` (Python 3.10 or later, with `kokoro_onnx`, `soundfile`, and Playwright for Python with its Chromium), `ffmpeg` and `ffprobe`. The Kokoro model files are in the folder that `os.environ["DEMO_TOOLS"]` names. **Install nothing.** Check them first:

```bash
demo-python --version
ffmpeg -version
ffprobe -version
```

Any other command is refused here, so:

- **Every command starts with `demo-python`, `ffmpeg`, `ffprobe`, `curl`, `mkdir` or `ls`.** Work in the current folder.
- **No shell variables, no `$`, no backquotes, no `~` and no loops.** Write every file name and number into the command itself: a mark from `marks.json`, a duration plus 0.6 s, a chapter's start. One command per segment is fine.
- **Python goes in a file** you write with the Write tool, and runs as `demo-python name.py`: the narration, the Playwright clips, the slide screenshots and the logo PNG. Inside Python, `subprocess.run(["ffmpeg", ...], check=True)` is fine.
- Read and write files with the Read and Write tools, not the shell.
- Read a duration with `ffprobe -v error -show_entries format=duration -of csv=p=0 demo.mp4` (any audio or video file) and a size with `ls -l demo.mp4`.

If a check fails, end your run with the exact failure as your final response. Don't install anything or work around it.
