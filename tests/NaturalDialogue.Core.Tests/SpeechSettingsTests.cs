using NaturalDialogue.Core;
using Xunit;

namespace NaturalDialogue.Core.Tests;

public class SpeechSettingsTests
{
    [Fact]
    public void Defaults_are_enabled_full_volume_and_normal_rate()
    {
        var settings = new SpeechSettings();

        Assert.True(settings.IsEnabled);
        Assert.Equal(SpeechSettings.MaxVolume, settings.Volume);
        Assert.Equal(0, settings.Rate);
        Assert.Null(settings.SelectedMaleVoice);
        Assert.Null(settings.SelectedFemaleVoice);
    }

    [Theory]
    [InlineData(-50, SpeechSettings.MinVolume)]
    [InlineData(0, 0)]
    [InlineData(42, 42)]
    [InlineData(100, 100)]
    [InlineData(500, SpeechSettings.MaxVolume)]
    public void Volume_is_clamped(int value, int expected)
    {
        var settings = new SpeechSettings { Volume = value };

        Assert.Equal(expected, settings.Volume);
        Assert.Equal(expected, SpeechSettings.ClampVolume(value));
    }

    [Theory]
    [InlineData(-99, SpeechSettings.MinRate)]
    [InlineData(-10, -10)]
    [InlineData(3, 3)]
    [InlineData(10, 10)]
    [InlineData(99, SpeechSettings.MaxRate)]
    public void Rate_is_clamped(int value, int expected)
    {
        var settings = new SpeechSettings { Rate = value };

        Assert.Equal(expected, settings.Rate);
        Assert.Equal(expected, SpeechSettings.ClampRate(value));
    }
}
