using NAudio.Wave;

namespace QuickSIP.Services;

public sealed record AudioDeviceOption(int Index, string Name)
{
    public override string ToString() => Index < 0 ? Name : $"{Index}: {Name}";
}

public static class AudioDeviceEnumerator
{
    public static IReadOnlyList<AudioDeviceOption> Inputs()
    {
        var list = new List<AudioDeviceOption> { new(-1, "System default") };
        try
        {
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                list.Add(new AudioDeviceOption(i, WaveIn.GetCapabilities(i).ProductName));
            }
        }
        catch
        {
            // No capture devices, or not running on Windows audio.
        }

        return list;
    }

    public static IReadOnlyList<AudioDeviceOption> Outputs()
    {
        var list = new List<AudioDeviceOption> { new(-1, "System default") };
        try
        {
            for (int i = 0; i < WaveOut.DeviceCount; i++)
            {
                list.Add(new AudioDeviceOption(i, WaveOut.GetCapabilities(i).ProductName));
            }
        }
        catch
        {
            // No render devices, or not running on Windows audio.
        }

        return list;
    }
}
