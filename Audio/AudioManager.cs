using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Serilog;

namespace SoundBox.Audio;

public sealed class AudioManager : IDisposable
{
    private readonly object _sync = new();
    private readonly ILogger _logger;
    private readonly WindowsPlaybackDevices _devices = new();
    private string? _monitorDeviceId = WindowsPlaybackDevices.DefaultDeviceId;
    private int _monitorVolume = 100;
    private PlaybackSession? _current;

    public AudioManager(ILogger logger)
    {
        _logger = logger.ForContext<AudioManager>();
    }

    public string? MonitorDeviceId => _monitorDeviceId;

    public int MonitorVolume => _monitorVolume;

    public void SetMonitorSettings(string? deviceId, int volume)
    {
        _monitorDeviceId = string.IsNullOrWhiteSpace(deviceId)
            ? WindowsPlaybackDevices.DefaultDeviceId
            : deviceId;
        _monitorVolume = Math.Clamp(volume, 0, 100);
    }

    public IReadOnlyList<AudioDevice> GetOutputDevices()
    {
        return _devices.Enumerate()
            .Select(device => new AudioDevice(device.Id, device.Name))
            .ToArray();
    }

    public bool Play(string filePath, string? outputDeviceId, bool monitor, int volumePercent, bool loop)
    {
        if (!File.Exists(filePath))
        {
            _logger.Warning("Sound file does not exist: {FilePath}", filePath);
            return false;
        }

        Stop();

        try
        {
            var outputs = new List<MMDevice>();
            var monitorDeviceAdded = false;
            var selectedOutput = _devices.Resolve(outputDeviceId ?? WindowsPlaybackDevices.DefaultDeviceId);
            if (selectedOutput is null)
            {
                _logger.Warning("Selected playback device unavailable: {DeviceId}", outputDeviceId);
                return false;
            }

            var monitorOutput = monitor ? _devices.Resolve(_monitorDeviceId) : null;
            if (monitor && monitorOutput is null)
            {
                _logger.Warning("Selected monitor device unavailable: {DeviceId}", _monitorDeviceId);
            }

            if (selectedOutput is not null)
            {
                outputs.Add(selectedOutput);
            }

            if (monitorOutput is not null)
            {
                if (outputs.All(device => !string.Equals(device.ID, monitorOutput.ID, StringComparison.OrdinalIgnoreCase)))
                {
                    outputs.Add(monitorOutput);
                    monitorDeviceAdded = true;
                }
            }

            if (outputs.Count == 0)
            {
                _logger.Warning("No active audio output device is available.");
                return false;
            }

            var session = new PlaybackSession(
                filePath,
                outputs,
                Math.Clamp(volumePercent, 0, 100) / 100f,
                _monitorVolume / 100f,
                monitorDeviceAdded ? monitorOutput?.ID : null,
                loop,
                _logger);
            lock (_sync)
            {
                _current = session;
            }

            session.Start();
            return true;
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or ArgumentException or NotSupportedException or COMException)
        {
            _logger.Error(exception, "Unable to start sound playback for {FilePath}", filePath);
            return false;
        }
    }

    public void Stop()
    {
        PlaybackSession? session;
        lock (_sync)
        {
            session = _current;
            _current = null;
        }

        session?.Dispose();
    }

    public void Dispose()
    {
        Stop();
        _devices.Dispose();
    }

    public sealed record AudioDevice(string Id, string Name);

    private sealed class PlaybackSession : IDisposable
    {
        private readonly string _filePath;
        private readonly IReadOnlyList<MMDevice> _outputs;
        private readonly float _volume;
        private readonly float _monitorVolume;
        private readonly string? _monitorDeviceId;
        private readonly bool _loop;
        private readonly ILogger _logger;
        private readonly List<(AudioFileReader Reader, WasapiOut Output)> _players = [];
        private int _stopping;

        public PlaybackSession(
            string filePath,
            IReadOnlyList<MMDevice> outputs,
            float volume,
            float monitorVolume,
            string? monitorDeviceId,
            bool loop,
            ILogger logger)
        {
            _filePath = filePath;
            _outputs = outputs;
            _volume = volume;
            _monitorVolume = monitorVolume;
            _monitorDeviceId = monitorDeviceId;
            _loop = loop;
            _logger = logger;
        }

        public void Start()
        {
            try
            {
                foreach (var device in _outputs)
                {
                    var reader = new AudioFileReader(_filePath)
                    {
                        Volume = string.Equals(device.ID, _monitorDeviceId, StringComparison.OrdinalIgnoreCase)
                            ? _monitorVolume
                            : _volume
                    };
                    var output = new WasapiOut(device, AudioClientShareMode.Shared, true, 100);
                    output.PlaybackStopped += (_, _) => HandlePlaybackStopped(reader, output);
                    _players.Add((reader, output));
                    output.Init(reader);
                    output.Play();
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private void HandlePlaybackStopped(AudioFileReader reader, WasapiOut output)
        {
            if (_loop && Volatile.Read(ref _stopping) == 0)
            {
                try
                {
                    reader.Position = 0;
                    output.Play();
                    return;
                }
                catch (Exception exception) when (exception is IOException or InvalidOperationException)
                {
                    _logger.Warning(exception, "Unable to loop sound playback.");
                }
            }

            if (_players.All(player => player.Output.PlaybackState == PlaybackState.Stopped))
            {
                Dispose();
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _stopping, 1) != 0)
            {
                return;
            }

            foreach (var (reader, output) in _players)
            {
                output.Stop();
                output.Dispose();
                reader.Dispose();
            }

            _players.Clear();
            foreach (var device in _outputs)
            {
                device.Dispose();
            }
        }
    }
}
