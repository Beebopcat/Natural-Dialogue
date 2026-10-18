using NaturalDialogue.Core;
using Xunit;

namespace NaturalDialogue.Core.Tests;

public class VoiceCatalogTests
{
    private static VoiceDescriptor Voice(string name, string culture = "en-GB", SpeakerGender gender = SpeakerGender.Male) =>
        new(name, culture, gender);

    [Fact]
    public void Build_groups_voices_of_the_requested_culture_by_gender()
    {
        var catalog = VoiceCatalog.Build(new[]
        {
            Voice("Ryan", gender: SpeakerGender.Male),
            Voice("Sonia", gender: SpeakerGender.Female),
            Voice("Thomas", gender: SpeakerGender.Male),
        });

        Assert.Equal(new[] { "Ryan", "Thomas" }, catalog.MaleVoices);
        Assert.Equal(new[] { "Sonia" }, catalog.FemaleVoices);
        Assert.False(catalog.IsEmpty);
    }

    [Fact]
    public void Build_excludes_other_cultures()
    {
        var catalog = VoiceCatalog.Build(new[]
        {
            Voice("Guy", culture: "en-US"),
            Voice("Ryan"),
        });

        Assert.Equal(new[] { "Ryan" }, catalog.MaleVoices);
    }

    [Fact]
    public void Build_matches_the_culture_case_insensitively()
    {
        var catalog = VoiceCatalog.Build(new[] { Voice("Ryan", culture: "EN-gb") });

        Assert.Equal(new[] { "Ryan" }, catalog.MaleVoices);
    }

    [Fact]
    public void Build_honours_a_custom_culture()
    {
        var catalog = VoiceCatalog.Build(
            new[] { Voice("Guy", culture: "en-US"), Voice("Ryan") },
            "en-US");

        Assert.Equal(new[] { "Guy" }, catalog.MaleVoices);
    }

    [Fact]
    public void Build_skips_duplicates_nulls_blank_names_and_unknown_genders()
    {
        var catalog = VoiceCatalog.Build(new VoiceDescriptor?[]
        {
            Voice("Ryan"),
            Voice("ryan"),
            null,
            Voice("  "),
            Voice("Narrator", gender: SpeakerGender.Unknown),
        }!);

        Assert.Equal(new[] { "Ryan" }, catalog.MaleVoices);
        Assert.Empty(catalog.FemaleVoices);
    }

    [Fact]
    public void Build_returns_an_empty_catalog_when_nothing_matches()
    {
        var catalog = VoiceCatalog.Build(Array.Empty<VoiceDescriptor>());

        Assert.True(catalog.IsEmpty);
        Assert.Empty(catalog.MaleVoices);
        Assert.Empty(catalog.FemaleVoices);
    }

    [Fact]
    public void Build_rejects_null_voices_and_empty_cultures()
    {
        Assert.Throws<ArgumentNullException>(() => VoiceCatalog.Build(null!));
        Assert.Throws<ArgumentException>(() => VoiceCatalog.Build(Array.Empty<VoiceDescriptor>(), " "));
    }

    [Fact]
    public void DefaultVoiceFor_returns_the_first_voice_of_each_gender()
    {
        var catalog = VoiceCatalog.Build(new[]
        {
            Voice("Ryan"),
            Voice("Thomas"),
            Voice("Sonia", gender: SpeakerGender.Female),
        });

        Assert.Equal("Ryan", catalog.DefaultVoiceFor(SpeakerGender.Male));
        Assert.Equal("Sonia", catalog.DefaultVoiceFor(SpeakerGender.Female));
        Assert.Null(catalog.DefaultVoiceFor(SpeakerGender.Unknown));
    }

    [Fact]
    public void DefaultVoiceFor_returns_null_when_the_gender_has_no_voices()
    {
        var catalog = VoiceCatalog.Build(new[] { Voice("Ryan") });

        Assert.Null(catalog.DefaultVoiceFor(SpeakerGender.Female));
    }

    [Theory]
    [InlineData("Ryan", true)]
    [InlineData("sonia", true)]
    [InlineData("Guy", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Contains_reports_whether_a_voice_is_available(string? name, bool expected)
    {
        var catalog = VoiceCatalog.Build(new[]
        {
            Voice("Ryan"),
            Voice("Sonia", gender: SpeakerGender.Female),
        });

        Assert.Equal(expected, catalog.Contains(name));
    }

    [Fact]
    public void VoiceDescriptor_rejects_null_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => new VoiceDescriptor(null!, "en-GB", SpeakerGender.Male));
        Assert.Throws<ArgumentNullException>(() => new VoiceDescriptor("Ryan", null!, SpeakerGender.Male));
    }
}
