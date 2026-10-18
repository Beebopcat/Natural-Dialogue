using NaturalDialogue.Core;
using Xunit;

namespace NaturalDialogue.Core.Tests;

public class DialoguePlannerTests
{
    private static SpeechSettings Settings() => new()
    {
        IsEnabled = true,
        SelectedMaleVoice = "Ryan",
        SelectedFemaleVoice = "Sonia",
        Volume = 80,
        Rate = 2,
    };

    [Fact]
    public void Plan_builds_a_request_for_a_known_male_speaker()
    {
        var request = new DialoguePlanner().Plan(Settings(), isDialogueChannel: true, "Thancred", "Hark!");

        Assert.Equal(new SpeechRequest("Ryan", "Hark!", 80, 2), request);
    }

    [Fact]
    public void Plan_builds_a_request_for_a_known_female_speaker()
    {
        var request = new DialoguePlanner().Plan(Settings(), isDialogueChannel: true, "Y'shtola", "Indeed.");

        Assert.Equal("Sonia", request!.VoiceName);
    }

    [Fact]
    public void Plan_trims_the_speaker_name_and_dialogue_text()
    {
        var request = new DialoguePlanner().Plan(Settings(), isDialogueChannel: true, "  Thancred ", "  Hark!  ");

        Assert.Equal("Hark!", request!.Text);
    }

    [Fact]
    public void Plan_returns_null_when_the_plugin_is_disabled()
    {
        var settings = Settings();
        settings.IsEnabled = false;

        Assert.Null(new DialoguePlanner().Plan(settings, isDialogueChannel: true, "Thancred", "Hark!"));
    }

    [Fact]
    public void Plan_returns_null_for_non_dialogue_channels()
    {
        Assert.Null(new DialoguePlanner().Plan(Settings(), isDialogueChannel: false, "Thancred", "Hark!"));
    }

    [Theory]
    [InlineData(null, "Hark!")]
    [InlineData("", "Hark!")]
    [InlineData("   ", "Hark!")]
    [InlineData("Thancred", null)]
    [InlineData("Thancred", "")]
    [InlineData("Thancred", "  ")]
    public void Plan_returns_null_for_empty_speaker_or_text(string? speaker, string? text)
    {
        Assert.Null(new DialoguePlanner().Plan(Settings(), isDialogueChannel: true, speaker, text));
    }

    [Fact]
    public void Plan_returns_null_for_an_unknown_speaker()
    {
        Assert.Null(new DialoguePlanner().Plan(Settings(), isDialogueChannel: true, "Wandering Minstrel", "Hark!"));
    }

    [Fact]
    public void Plan_returns_null_when_the_matching_voice_is_not_configured()
    {
        var settings = Settings();
        settings.SelectedMaleVoice = "";

        Assert.Null(new DialoguePlanner().Plan(settings, isDialogueChannel: true, "Thancred", "Hark!"));
        Assert.NotNull(new DialoguePlanner().Plan(settings, isDialogueChannel: true, "Y'shtola", "Indeed."));
    }

    [Fact]
    public void Plan_uses_a_custom_character_directory()
    {
        var directory = new CharacterGenderDirectory(new Dictionary<string, SpeakerGender>
        {
            { "Ardbert", SpeakerGender.Male },
        });
        var planner = new DialoguePlanner(directory);

        Assert.Same(directory, planner.Characters);
        Assert.NotNull(planner.Plan(Settings(), isDialogueChannel: true, "Ardbert", "Hark!"));
        Assert.Null(planner.Plan(Settings(), isDialogueChannel: true, "Thancred", "Hark!"));
    }

    [Fact]
    public void Plan_rejects_null_settings()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new DialoguePlanner().Plan(null!, isDialogueChannel: true, "Thancred", "Hark!"));
    }

    [Theory]
    [InlineData(SpeakerGender.Male, "Ryan")]
    [InlineData(SpeakerGender.Female, "Sonia")]
    [InlineData(SpeakerGender.Unknown, null)]
    public void SelectVoice_maps_gender_to_the_configured_voice(SpeakerGender gender, string? expected)
    {
        Assert.Equal(expected, DialoguePlanner.SelectVoice(Settings(), gender));
    }

    [Fact]
    public void SelectVoice_treats_blank_configuration_as_unset()
    {
        var settings = Settings();
        settings.SelectedFemaleVoice = "   ";

        Assert.Null(DialoguePlanner.SelectVoice(settings, SpeakerGender.Female));
    }

    [Fact]
    public void SelectVoice_rejects_null_settings()
    {
        Assert.Throws<ArgumentNullException>(() => DialoguePlanner.SelectVoice(null!, SpeakerGender.Male));
    }
}
