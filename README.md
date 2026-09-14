# SoundBox

A Windows soundboard plugin for [Macro Deck 3](https://macrodeck.org/). Trigger WAV and MP3 files from your Macro Deck buttons and route playback to a selected Windows audio output.

## Features

- Play WAV and MP3 files from Macro Deck actions
- Select the Windows playback device for each sound
- Use the default Windows playback device when needed
- Adjust playback volume from 0% to 100%
- Loop sounds until they are stopped
- Monitor playback through a separate configured device
- Stop the currently playing SoundBox sound
- Refresh available playback devices while configuring an action

## Requirements

- Windows
- Macro Deck 3.0.0-beta.5 or newer
- .NET 10 SDK to build from source

## Installation

1. Download the latest SoundBox release.
2. Extract the plugin package.
3. Copy the package into your Macro Deck plugins directory.
4. Restart Macro Deck.
5. Add the **Play Sound** or **Stop Sound** action to a button.

## Building

From the repository directory, run:

```powershell
dotnet restore
dotnet build -c Release
dotnet publish SoundBox.csproj -c Release -r win-x64 --self-contained true -o bin/publish/win-x64
```

The published plugin is generated in `bin/publish/win-x64`.

## Configuration

The **Play Sound** action supports:

- Sound file: WAV or MP3
- Output device: a specific Windows playback device or the default device
- Monitor sound: optionally mirror playback to the configured monitor device
- Volume: 0-100%
- Loop: restart the sound automatically when it ends

The monitor device and monitor volume can be configured through the plugin configuration flow in Macro Deck.

## Support

If you find a problem or have a feature suggestion, please open an issue in this repository with:

- Your Macro Deck version
- Your Windows version
- The sound file format
- The selected output device
- Relevant Macro Deck log details

## License

SoundBox is licensed under the [MIT License](LICENSE).

<img width="1436" height="854" alt="image" src="https://github.com/user-attachments/assets/6b205ede-a8e8-4f60-9c2d-653e2242b393" />


<img width="748" height="365" alt="image" src="https://github.com/user-attachments/assets/05642ab4-cfde-41a6-b263-66d25d113a65" />

<img width="772" height="264" alt="image" src="https://github.com/user-attachments/assets/9e703a26-fa35-431f-ae2d-31afb35ca5d9" />
