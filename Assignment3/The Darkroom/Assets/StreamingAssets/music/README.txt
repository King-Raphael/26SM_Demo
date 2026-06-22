BACKGROUND MUSIC
================

Drop ONE music track in this folder and it becomes the game's looping
background music — the melody over the evolving ambient bed.

Supported formats:  .ogg  (recommended)  /  .wav  /  .mp3
The FIRST audio file found here (alphabetical) is used. Name it anything.

It is loaded at runtime by AudioDirector.LoadMusic() straight from
StreamingAssets (no Unity import / .meta needed — it's raw file streaming).
If no file is present, the game simply plays no music (graceful degrade,
same as the external art).

How it sits in the mix (all tunable in AudioDirector):
  - Volume: AudioDirector.MusicVolume (default 0.20). Lower it if the track
    fights the hum / hiss / mood drone; raise it to bring the melody forward.
  - The prologue (Frame 0) stays music-free so its intimate open breathes;
    music fades in when you step into Frame 1.
  - It ducks with the rest of the ambience during the Room 9 blackout, and
    falls silent for the finale's held breath and the win screen.

To use it:
  1. Copy your track into this folder (e.g. theme.ogg).
  2. Press Play (Cmd-P) in the Editor. (No Cmd-R needed — it's not a Unity
     asset import; it's read directly from disk.)
