namespace NaturalDialogue.Core;

/// <summary>
/// Voice and audio settings shared by the plugin configuration and the dialogue planner.
/// </summary>
public interface ISpeechSettings
{
    bool IsEnabled { get; }

    string? SelectedMaleVoice { get; }

    string? SelectedFemaleVoice { get; }

    int Volume { get; }

    int Rate { get; }
}

public sealed class SpeechSettings : ISpeechSettings
{
    public const int MinVolume = 0;
    public const int MaxVolume = 100;
    public const int MinRate = -10;
    public const int MaxRate = 10;

    private int _volume = MaxVolume;
    private int _rate = 0;

    public bool IsEnabled { get; set; } = true;

    public string? SelectedMaleVoice { get; set; }

    public string? SelectedFemaleVoice { get; set; }

    public int Volume
    {
        get => _volume;
        set => _volume = ClampVolume(value);
    }

    public int Rate
    {
        get => _rate;
        set => _rate = ClampRate(value);
    }

    public static int ClampVolume(int value) => Math.Clamp(value, MinVolume, MaxVolume);

    public static int ClampRate(int value) => Math.Clamp(value, MinRate, MaxRate);
}
