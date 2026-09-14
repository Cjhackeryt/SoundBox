using System.Runtime.InteropServices;
using MacroDeck.Localization;
using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;
using Serilog;
using SoundBox.Audio;

namespace SoundBox.Actions;

public sealed class PlaySoundAction : IDynamicOptionsActionDefinition
{
    private const string SoundFile = "soundFile";
    private const string OutputDevice = "outputDevice";
    private const string Monitor = "monitor";
    private const string Volume = "volume";
    private const string Loop = "loop";
    private readonly AudioManager _audioManager;
    private readonly ILogger _logger;

    public PlaySoundAction(AudioManager audioManager, ILogger logger)
    {
        _audioManager = audioManager;
        _logger = logger.ForContext<PlaySoundAction>();
    }

    public string Id => "play-sound";
    public LocalizedText Name => "Play Sound";
    public LocalizedText Description => "Plays a sound through a selected Windows audio output.";
    public MacroDeckPlatform Platforms => MacroDeckPlatform.Windows;

    public IReadOnlyList<ActionParameter> Parameters =>
    [
        ActionParameter.File(SoundFile, "Sound File", "WAV and MP3 files are supported.", ["wav", "mp3"], true),
        ActionParameter.DynamicChoice(OutputDevice, "Output Device", "The Windows output device to receive the sound.", required: true),
        ActionParameter.Toggle(Monitor, "Monitor Sound", "Also play through SoundBox's universal monitor playback device.", true),
        ActionParameter.Slider(Volume, 0, 100, "Volume", "Playback volume.", 1, 100),
        ActionParameter.Toggle(Loop, "Loop", "Restart the sound automatically when it ends.")
    ];

    public IActionExecutor CreateExecutor() => new Executor(_audioManager, _logger);

    public Task<DynamicOptionsResult> GetDynamicOptionsAsync(DynamicOptionsContext context, CancellationToken cancellationToken)
    {
        if (!string.Equals(context.ParameterName, OutputDevice, StringComparison.Ordinal))
        {
            return Task.FromResult(new DynamicOptionsResult { Options = [] });
        }

        var selectedDeviceId = context.CurrentParameters.TryGetValue(OutputDevice, out var selectedValue)
            ? selectedValue?.ToString()
            : null;
        var options = new List<ActionParameterOption>
        {
            new()
            {
                Value = WindowsPlaybackDevices.DefaultDeviceId,
                Label = "Default Windows Playback Device"
            }
        };

        try
        {
            options.AddRange(_audioManager.GetOutputDevices()
                .Select(device => new ActionParameterOption { Value = device.Id, Label = device.Name }));
        }
        catch (Exception exception) when (exception is COMException or InvalidOperationException or ArgumentException)
        {
            _logger.Warning(exception, "Unable to enumerate Windows audio output devices while configuring Play Sound.");
        }

        if (!string.IsNullOrWhiteSpace(selectedDeviceId) &&
            !string.Equals(selectedDeviceId, WindowsPlaybackDevices.DefaultDeviceId, StringComparison.OrdinalIgnoreCase) &&
            options.All(option => !string.Equals(option.Value, selectedDeviceId, StringComparison.OrdinalIgnoreCase)))
        {
            options.Add(new ActionParameterOption
            {
                Value = selectedDeviceId,
                Label = "Selected playback device unavailable"
            });
        }

        return Task.FromResult(new DynamicOptionsResult
        {
            Options = options,
            CacheSeconds = 5
        });
    }

    private sealed class Executor : IActionExecutor
    {
        private readonly AudioManager _audioManager;
        private readonly ILogger _logger;

        public Executor(AudioManager audioManager, ILogger logger)
        {
            _audioManager = audioManager;
            _logger = logger;
        }

        public Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
        {
            var filePath = GetString(context, SoundFile);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return Task.FromResult(ActionResult.Failed(ActionErrorCodes.InvalidParameter, "A sound file is required."));
            }

            var outputDevice = GetString(context, OutputDevice);
            var monitor = GetBool(context, Monitor, true);
            var volume = Math.Clamp(GetInt(context, Volume, 100), 0, 100);
            var loop = GetBool(context, Loop);

            try
            {
                return _audioManager.Play(filePath, outputDevice, monitor, volume, loop)
                    ? ActionResult.SucceededTask
                    : Task.FromResult(ActionResult.Failed(ActionErrorCodes.Unavailable, "Playback device unavailable."));
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException or ArgumentException or NotSupportedException or COMException)
            {
                _logger.Error(exception, "Sound playback failed for {FilePath}", filePath);
                return Task.FromResult(ActionResult.Failed(ActionErrorCodes.Unavailable, "The sound could not be played."));
            }
        }

        private static string? GetString(ActionExecutionContext context, string name) =>
            context.Parameters.TryGetValue(name, out var value) ? value?.ToString() : null;

        private static bool GetBool(ActionExecutionContext context, string name, bool defaultValue = false) =>
            context.Parameters.TryGetValue(name, out var value) && value is not null
                ? Convert.ToBoolean(value, System.Globalization.CultureInfo.InvariantCulture)
                : defaultValue;

        private static int GetInt(ActionExecutionContext context, string name, int defaultValue)
        {
            if (!context.Parameters.TryGetValue(name, out var value) || value is null)
            {
                return defaultValue;
            }

            try
            {
                return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
            {
                return defaultValue;
            }
        }
    }
}
