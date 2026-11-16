Save the script with the Write tool as `narrate.py` and run it with `demo-python narrate.py`. Then level each WAV with its own command, every file name written out, one line per segment:

```bash
ffmpeg -hide_banner -v error -y -i s01.wav -af loudnorm=I=-16:TP=-1.5:LRA=11 -ar 24000 s01-n.wav
ffmpeg -hide_banner -v error -y -i s02.wav -af loudnorm=I=-16:TP=-1.5:LRA=11 -ar 24000 s02-n.wav
```

Or level them at the end of `narrate.py`, one call per segment: `subprocess.run(["ffmpeg", "-hide_banner", "-v", "error", "-y", "-i", name + ".wav", "-af", "loudnorm=I=-16:TP=-1.5:LRA=11", "-ar", "24000", name + "-n.wav"], check=True)`.
