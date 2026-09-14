using System.Runtime.InteropServices;
using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.ConfigFlow;
using Serilog;
using SoundBox.Audio;

namespace SoundBox.ConfigFlow;

internal sealed class MonitorSettingsFlow : IConfigFlow
{
    internal const string MonitorDeviceIdField = "monitorDeviceId";
    internal const string MonitorVolumeField = "monitorVolume";

    private const string StepId = "monitor-settings";
    private readonly AudioManager _audioManager;
    private readonly ILogger _logger;

    public MonitorSettingsFlow(AudioManager audioManager, ILogger logger)
    {
        _audioManager = audioManager;
        _logger = logger.ForContext<MonitorSettingsFlow>();
    }

    public Task<ConfigFlowResult> StartAsync(IConfigFlowContext context, CancellationToken cancellationToken)
    {
        _logger.Information("[SoundBox-Config] Monitor configuration opened");
        return Task.FromResult(ConfigFlowResult.Step(BuildStep()));
    }

    public Task<ConfigFlowResult> SubmitAsync(
        string stepId,
        IReadOnlyDictionary<string, object?> input,
        IConfigFlowContext context,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(stepId, StepId, StringComparison.Ordinal))
        {
            _logger.Error("[SoundBox-Config-ERROR] Unknown configuration step: {StepId}", stepId);
            return Task.FromResult(ConfigFlowResult.Error(BuildStep(), "Unknown configuration step."));
        }

        var deviceId = input.GetValueOrDefault(MonitorDeviceIdField)?.ToString();
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            _logger.Error("[SoundBox-Config-ERROR] Monitor device selection was empty.");
            return Task.FromResult(ConfigFlowResult.Error(
                BuildStep(),
                "Select a monitor playback device.",
                new Dictionary<string, LocalizedText>
                {
                    [MonitorDeviceIdField] = "A monitor playback device is required."
                }));
        }

        int volume;
        try
        {
            volume = input.GetValueOrDefault(MonitorVolumeField) is null
                ? 100
                : Convert.ToInt32(input[MonitorVolumeField], System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            _logger.Error(exception, "[SoundBox-Config-ERROR] Invalid monitor volume.");
            return Task.FromResult(ConfigFlowResult.Error(BuildStep(), "Monitor volume is invalid."));
        }

        _audioManager.SetMonitorSettings(deviceId, volume);
        _logger.Information("[SoundBox-Config] Monitor device saved: ID = {DeviceId}", deviceId);
        return Task.FromResult(ConfigFlowResult.Complete("SoundBox Monitor"));
    }

    private ConfigFlowStep BuildStep()
    {
        var devices = new List<ActionParameterOption>
        {
            new()
            {
                Value = WindowsPlaybackDevices.DefaultDeviceId,
                Label = "Default Windows Playback Device"
            }
        };

        try
        {
            var availableDevices = AudioManager.GetOutputDevices();
            _logger.Information("[SoundBox-Config] Enumerated playback devices: {Count}", availableDevices.Count);
            devices.AddRange(availableDevices
                .Select(device => new ActionParameterOption { Value = device.Id, Label = device.Name }));
        }
        catch (Exception exception) when (exception is COMException or InvalidOperationException)
        {
            _logger.Error(exception, "[SoundBox-Config-ERROR] Unable to enumerate playback devices.");
        }

        var selectedId = _audioManager.MonitorDeviceId ?? WindowsPlaybackDevices.DefaultDeviceId;
        _logger.Information("[SoundBox-Config] Saved monitor device ID: {DeviceId}", selectedId);
        if (!string.Equals(selectedId, WindowsPlaybackDevices.DefaultDeviceId, StringComparison.OrdinalIgnoreCase) &&
            devices.All(device => device.Value != selectedId))
        {
            devices.Add(new ActionParameterOption { Value = selectedId, Label = "Selected monitor device unavailable" });
            _logger.Warning("[SoundBox-Config] Resolved monitor device: unavailable ({DeviceId})", selectedId);
        }
        else
        {
            var selectedDevice = devices.FirstOrDefault(device => device.Value == selectedId);
            _logger.Information(
                "[SoundBox-Config] Resolved monitor device: {Name}",
                selectedDevice?.Label.ToString() ?? "Default Windows Playback Device");
        }

        return new ConfigFlowStep
        {
            StepId = StepId,
            Title = "SoundBox Settings",
            Description = "Choose the universal playback device and volume used when Monitor Sound is enabled.",
            Fields =
            [
                ActionParameter.Choice(
                    MonitorDeviceIdField,
                    devices,
                    label: "Monitor Playback Device",
                    description: "All monitored SoundBox sounds use this Windows playback device.",
                    defaultValue: selectedId,
                    required: true),
                ActionParameter.Slider(
                    MonitorVolumeField,
                    0,
                    100,
                    label: "Monitor Volume",
                    description: "Global volume for the monitor playback device.",
                    step: 1,
                    defaultValue: _audioManager.MonitorVolume)
            ]
        };
    }
}
