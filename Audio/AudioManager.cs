using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Serilog;

namespace SoundBox.Audio;

public sealed class AudioManager : IDisposable
{
    private readonly object _sync = new();
    private readonly ILogger _logger;
    private readonly MMDeviceEnumerator _deviceEnumerator = new();
    private PlaybackSession? _current;

    public AudioManager(ILogger logger)
    {
        _logger = logger.ForContext<AudioManager>();
    }

    public IReadOnlyList<AudioDevice> GetOutputDevices()
    {
        return _deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .Select(device => new AudioDevice(device.ID, device.FriendlyName))
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
            var selectedOutput = FindDevice(outputDeviceId);
            if (selectedOutput is not null)
            {
                outputs.Add(selectedOutput);
            }

            if (monitor)
            {
                var monitorOutput = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                if (outputs.All(device => !string.Equals(device.ID, monitorOutput.ID, StringComparison.OrdinalIgnoreCase)))
                {
                    outputs.Add(monitorOutput);
                }
            }

            if (outputs.Count == 0)
            {
                _logger.Warning("No active audio output device is available.");
                return false;
            }

            var session = new PlaybackSession(filePath, outputs, Math.Clamp(volumePercent, 0, 100) / 100f, loop, _logger);
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

    private MMDevice? FindDevice(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return null;
        }

        return _deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .FirstOrDefault(device => string.Equals(device.ID, deviceId, StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        Stop();
        _deviceEnumerator.Dispose();
    }

    public sealed record AudioDevice(string Id, string Name);

    private sealed class PlaybackSession : IDisposable
    {
        private readonly string _filePath;
        private readonly IReadOnlyList<MMDevice> _outputs;
        private readonly float _volume;
        private readonly bool _loop;
        private readonly ILogger _logger;
        private readonly List<(AudioFileReader Reader, WasapiOut Output)> _players = [];
        private int _stopping;

        public PlaybackSession(string filePath, IReadOnlyList<MMDevice> outputs, float volume, bool loop, ILogger logger)
        {
            _filePath = filePath;
            _outputs = outputs;
            _volume = volume;
            _loop = loop;
            _logger = logger;
        }

        public void Start()
        {
            try
            {
                foreach (var device in _outputs)
                {
                    var reader = new AudioFileReader(_filePath) { Volume = _volume };
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
