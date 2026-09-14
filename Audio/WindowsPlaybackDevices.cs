using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace SoundBox.Audio;

public sealed class WindowsPlaybackDevices
{
    public const string DefaultDeviceId = "DEFAULT";

    public static IReadOnlyList<PlaybackDevice> Enumerate()
    {
        using var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
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

    public static MMDevice? Resolve(string? deviceId)
    {
        using var enumerator = new MMDeviceEnumerator();
        try
        {
            if (string.Equals(deviceId, DefaultDeviceId, StringComparison.OrdinalIgnoreCase))
            {
                return enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            }

            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return null;
            }

            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
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

    public sealed record PlaybackDevice(string Id, string Name);
}
