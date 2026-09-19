# SoundBox

SoundBox is a Macro Deck 3 plugin for playing and stopping WAV or MP3 files through Windows audio output devices.

## Requirements

- Macro Deck 3.0.0-beta.11 or newer
- .NET 10 runtime, supplied by Macro Deck
- Windows audio output devices for playback

## Build

```powershell
dotnet build -c Release
macrodeck-plugin build --source . --output .\artifacts
```

The release package is framework-dependent and targets `win-x64`.

## Actions

- **Play Sound** selects a sound file, output device, volume, monitoring, and looping.
- **Stop Sound** stops the current playback.

SoundBox does not collect or transmit user data. It only accesses local Windows audio devices and the selected local sound file.
