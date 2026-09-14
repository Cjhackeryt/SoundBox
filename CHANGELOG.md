# Changelog

All notable changes to SoundBox are documented here.

## 1.1.1 (2026-09-15)

### Fixed

- Configuration dialog opened blank and the **Play Sound** action lost its output device list after the first configuration was saved. Device enumeration reused a disposed audio enumerator after the plugin was reinitialized, which crashed the configuration flow and dynamic device options. Each enumeration now creates a fresh enumerator, so device listing keeps working across reconfiguration.

## 1.1.0 (2026-09-15)

### Changed

- Updated to Macro Deck SDK 3.0.0-beta.5.
- Requires Macro Deck 3.0.0-beta.5 or newer.

## 1.0.0

### Added

- Initial release as a Macro Deck 3 plugin.
- Play WAV and MP3 sounds from Macro Deck buttons.
- Select a Windows playback device per sound, or use the default device.
- Adjust playback volume from 0% to 100%.
- Loop sounds until stopped.
- Mirror playback through a separately configured monitor device.
- Stop the currently playing SoundBox sound.
- Configuration flow for choosing the monitor playback device and volume.