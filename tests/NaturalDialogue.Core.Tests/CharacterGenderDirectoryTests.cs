using NaturalDialogue.Core;
using Xunit;

namespace NaturalDialogue.Core.Tests;

public class CharacterGenderDirectoryTests
{
    [Theory]
    [InlineData("Y'shtola", SpeakerGender.Female)]
    [InlineData("Thancred", SpeakerGender.Male)]
    [InlineData("G'raha Tia", SpeakerGender.Male)]
    [InlineData("Minfilia", SpeakerGender.Female)]
    public void Resolve_returns_gender_for_known_characters(string name, SpeakerGender expected)
    {
        Assert.Equal(expected, new CharacterGenderDirectory().Resolve(name));
    }

    [Theory]
    [InlineData("  Tataru  ")]
    [InlineData("tataru")]
    [InlineData("TATARU")]
    public void Resolve_ignores_case_and_surrounding_whitespace(string name)
    {
        Assert.Equal(SpeakerGender.Female, new CharacterGenderDirectory().Resolve(name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Wandering Minstrel")]
    public void Resolve_returns_unknown_for_missing_names(string? name)
    {
        Assert.Equal(SpeakerGender.Unknown, new CharacterGenderDirectory().Resolve(name));
    }

    [Fact]
    public void Register_adds_new_characters()
    {
        var directory = new CharacterGenderDirectory();
        var countBefore = directory.Count;

        directory.Register("Estinien", SpeakerGender.Male);

        Assert.Equal(countBefore + 1, directory.Count);
        Assert.Equal(SpeakerGender.Male, directory.Resolve("Estinien"));
    }

    [Fact]
    public void Register_overwrites_an_existing_character()
    {
        var directory = new CharacterGenderDirectory();

        directory.Register("Cid", SpeakerGender.Female);

        Assert.Equal(SpeakerGender.Female, directory.Resolve("Cid"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Register_rejects_empty_names(string? name)
    {
        var directory = new CharacterGenderDirectory();

        Assert.Throws<ArgumentException>(() => directory.Register(name!, SpeakerGender.Male));
    }

    [Fact]
    public void Custom_directory_only_contains_the_supplied_characters()
    {
        var directory = new CharacterGenderDirectory(new Dictionary<string, SpeakerGender>
        {
            { "Ardbert", SpeakerGender.Male },
        });

        Assert.Equal(1, directory.Count);
        Assert.Equal(SpeakerGender.Male, directory.Resolve("Ardbert"));
        Assert.Equal(SpeakerGender.Unknown, directory.Resolve("Thancred"));
    }

    [Fact]
    public void Constructor_rejects_null_characters()
    {
        Assert.Throws<ArgumentNullException>(() => new CharacterGenderDirectory(null!));
    }

    [Fact]
    public void Default_characters_are_not_empty()
    {
        Assert.NotEmpty(CharacterGenderDirectory.DefaultCharacters);
        Assert.Equal(CharacterGenderDirectory.DefaultCharacters.Count, new CharacterGenderDirectory().Count);
    }
}
