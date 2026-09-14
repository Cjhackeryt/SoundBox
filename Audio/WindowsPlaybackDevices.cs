using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace SoundBox.Audio;

public sealed class WindowsPlaybackDevices : IDisposable
{
    public const string DefaultDeviceId = "DEFAULT";

    private readonly MMDeviceEnumerator _enumerator = new();

    public IReadOnlyList<PlaybackDevice> Enumerate()
    {
        var devices = _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        try
        {
            return devices
                .Select(device => new PlaybackDevice(device.ID, device.FriendlyName))
                .ToArray();
        }
        finally
        {
            foreach (var device in devices)
            {
                device.Dispose();
            }
        }
    }

    public MMDevice? Resolve(string? deviceId)
    {
        try
        {
            if (string.Equals(deviceId, DefaultDeviceId, StringComparison.OrdinalIgnoreCase))
            {
                return _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            }

            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return null;
            }

            var devices = _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            MMDevice? match = null;
            foreach (var device in devices)
            {
                if (string.Equals(device.ID, deviceId, StringComparison.OrdinalIgnoreCase))
                {
                    match = device;
                }
                else
                {
                    device.Dispose();
                }
            }

            return match;
        }
        catch (COMException)
        {
            return null;
        }
    }

    public void Dispose() => _enumerator.Dispose();

    public sealed record PlaybackDevice(string Id, string Name);
}
