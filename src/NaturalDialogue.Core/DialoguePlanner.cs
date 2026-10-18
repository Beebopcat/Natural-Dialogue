namespace NaturalDialogue.Core;

/// <summary>
/// Turns a raw chat line into a <see cref="SpeechRequest"/>, or nothing when the line
/// should be ignored.
/// </summary>
public sealed class DialoguePlanner
{
    private readonly CharacterGenderDirectory _characters;

    public DialoguePlanner(CharacterGenderDirectory? characters = null)
    {
        _characters = characters ?? new CharacterGenderDirectory();
    }

    public CharacterGenderDirectory Characters => _characters;

    public SpeechRequest? Plan(ISpeechSettings settings, bool isDialogueChannel, string? speakerName, string? dialogueText)
    {
        if (settings is null) throw new ArgumentNullException(nameof(settings));

        if (!settings.IsEnabled) return null;
        if (!isDialogueChannel) return null;

        var speaker = speakerName?.Trim();
        var text = dialogueText?.Trim();

        if (string.IsNullOrWhiteSpace(speaker) || string.IsNullOrWhiteSpace(text)) return null;

        var voice = SelectVoice(settings, _characters.Resolve(speaker));
        if (voice is null) return null;

        return new SpeechRequest(voice, text!, settings.Volume, settings.Rate);
    }

    public static string? SelectVoice(ISpeechSettings settings, SpeakerGender gender)
    {
        if (settings is null) throw new ArgumentNullException(nameof(settings));

        var voice = gender switch
        {
            SpeakerGender.Male => settings.SelectedMaleVoice,
            SpeakerGender.Female => settings.SelectedFemaleVoice,
            _ => null,
        };

        return string.IsNullOrWhiteSpace(voice) ? null : voice;
    }
}
