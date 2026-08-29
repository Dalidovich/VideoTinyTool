# tools

Drop `ffprobe.exe` here.

`ffmpeg.exe` comes from the `NReco.VideoConverter` package and is copied to the output automatically.
`ffprobe.exe` is not part of that package, so the build stages it from this folder, or from `PATH` when this
folder is empty. Both binaries must sit next to `VideoTinyTool.exe` for the application to read media files.

Any build of ffmpeg 6 or newer works. This folder is not part of the shipped application.
