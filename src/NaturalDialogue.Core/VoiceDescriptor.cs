namespace NaturalDialogue.Core;

/// <summary>
/// Platform independent description of an installed text-to-speech voice.
/// </summary>
public sealed class VoiceDescriptor
{
    public VoiceDescriptor(string name, string cultureName, SpeakerGender gender)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        CultureName = cultureName ?? throw new ArgumentNullException(nameof(cultureName));
        Gender = gender;
    }

    public string Name { get; }

    public string CultureName { get; }

    public SpeakerGender Gender { get; }
}
