namespace NaturalDialogue.Core;

/// <summary>
/// Maps a speaker name to the gender used for voice selection.
/// </summary>
public sealed class CharacterGenderDirectory
{
    private readonly Dictionary<string, SpeakerGender> _genderByName;

    public CharacterGenderDirectory()
        : this(DefaultCharacters)
    {
    }

    public CharacterGenderDirectory(IEnumerable<KeyValuePair<string, SpeakerGender>> characters)
    {
        if (characters is null) throw new ArgumentNullException(nameof(characters));

        _genderByName = new Dictionary<string, SpeakerGender>(StringComparer.OrdinalIgnoreCase);
        foreach (var character in characters)
        {
            Register(character.Key, character.Value);
        }
    }

    public static IReadOnlyDictionary<string, SpeakerGender> DefaultCharacters { get; } =
        new Dictionary<string, SpeakerGender>(StringComparer.OrdinalIgnoreCase)
        {
            { "Y'shtola", SpeakerGender.Female },
            { "Thancred", SpeakerGender.Male },
            { "Urianger", SpeakerGender.Male },
            { "Alphinaud", SpeakerGender.Male },
            { "Alisaie", SpeakerGender.Female },
            { "G'raha Tia", SpeakerGender.Male },
            { "Krile", SpeakerGender.Female },
            { "Tataru", SpeakerGender.Female },
            { "Cid", SpeakerGender.Male },
            { "Lyse", SpeakerGender.Female },
            { "Hien", SpeakerGender.Male },
            { "Minfilia", SpeakerGender.Female },
        };

    public int Count => _genderByName.Count;

    /// <summary>
    /// Adds or replaces the gender for a speaker. Names are matched trimmed and case insensitively.
    /// </summary>
    public void Register(string speakerName, SpeakerGender gender)
    {
        if (string.IsNullOrWhiteSpace(speakerName))
        {
            throw new ArgumentException("Speaker name must not be empty.", nameof(speakerName));
        }

        _genderByName[speakerName.Trim()] = gender;
    }

    public SpeakerGender Resolve(string? speakerName)
    {
        if (string.IsNullOrWhiteSpace(speakerName))
        {
            return SpeakerGender.Unknown;
        }

        return _genderByName.TryGetValue(speakerName.Trim(), out var gender)
            ? gender
            : SpeakerGender.Unknown;
    }
}
