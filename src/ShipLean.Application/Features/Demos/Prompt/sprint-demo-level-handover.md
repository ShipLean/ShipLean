```bash
for f in s*.wav; do $FF -hide_banner -v error -y -i "$f" -af loudnorm=I=-16:TP=-1.5:LRA=11 -ar 24000 "${f%.wav}-n.wav"; done
```
