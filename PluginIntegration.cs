using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.ConfigFlow;
using Serilog;
using SoundBox.Actions;
using SoundBox.Audio;
using SoundBox.ConfigFlow;

namespace SoundBox;

public sealed class PluginIntegration : IPluginIntegration, IConfigFlowProvider, IDisposable
{
    private readonly AudioManager _audioManager;
    private readonly ILogger _logger;

    public PluginIntegration(ILogger logger)
    {
        _logger = logger.ForContext<PluginIntegration>();
        _audioManager = new AudioManager(logger);
        Actions =
        [
            new PlaySoundAction(_audioManager, logger),
            new StopSoundAction(_audioManager, logger)
        ];
    }

    public IReadOnlyList<IActionDefinition> Actions { get; }

    public async Task InitializeAsync(IIntegrationContext context)
    {
        var entries = await context.Config.GetEntriesAsync();
        if (entries.Count == 0)
        {
            return;
        }

        var entry = entries[0];
        var deviceId = await context.Config.GetStringAsync(entry.Id, MonitorSettingsFlow.MonitorDeviceIdField);
        var volume = await context.Config.GetStringAsync(entry.Id, MonitorSettingsFlow.MonitorVolumeField);
        _audioManager.SetMonitorSettings(
            deviceId,
            int.TryParse(volume, out var parsedVolume) ? parsedVolume : 100);
    }

    public Task ShutdownAsync()
    {
        _audioManager.Dispose();
        return Task.CompletedTask;
    }

    public bool AllowsMultipleConfigurations => false;

    public IConfigFlow CreateConfigFlow()
    {
        _logger.Information("[SoundBox-Config] Reconfigure requested");
        _logger.Information("[SoundBox-Config] Creating fresh configuration");
        _logger.Information("[SoundBox-Config] Configuration initialized successfully");
        return new MonitorSettingsFlow(_audioManager, _logger);
    }

    public void Dispose() => _audioManager.Dispose();
}
