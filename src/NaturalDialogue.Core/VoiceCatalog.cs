namespace NaturalDialogue.Core;

/// <summary>
/// Groups the installed voices of a single culture by gender, preserving install order
/// and ignoring duplicates.
/// </summary>
public sealed class VoiceCatalog
{
    public const string DefaultCulture = "en-GB";

    private VoiceCatalog(IReadOnlyList<string> maleVoices, IReadOnlyList<string> femaleVoices)
    {
        MaleVoices = maleVoices;
        FemaleVoices = femaleVoices;
    }

    public IReadOnlyList<string> MaleVoices { get; }

    public IReadOnlyList<string> FemaleVoices { get; }

    public bool IsEmpty => MaleVoices.Count == 0 && FemaleVoices.Count == 0;

    public static VoiceCatalog Build(IEnumerable<VoiceDescriptor> voices, string cultureName = DefaultCulture)
    {
        if (voices is null) throw new ArgumentNullException(nameof(voices));
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            throw new ArgumentException("Culture must not be empty.", nameof(cultureName));
        }

        var male = new List<string>();
        var female = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var voice in voices)
        {
            if (voice is null) continue;
            if (!string.Equals(voice.CultureName, cultureName, StringComparison.OrdinalIgnoreCase)) continue;
            if (string.IsNullOrWhiteSpace(voice.Name)) continue;
            if (!seen.Add(voice.Name)) continue;

            switch (voice.Gender)
            {
                case SpeakerGender.Male:
                    male.Add(voice.Name);
                    break;
                case SpeakerGender.Female:
                    female.Add(voice.Name);
                    break;
            }
        }

        return new VoiceCatalog(male, female);
    }

    /// <summary>
    /// The voice used when nothing is configured yet for the given gender.
    /// </summary>
    public string? DefaultVoiceFor(SpeakerGender gender) => gender switch
    {
        SpeakerGender.Male => MaleVoices.Count > 0 ? MaleVoices[0] : null,
        SpeakerGender.Female => FemaleVoices.Count > 0 ? FemaleVoices[0] : null,
        _ => null,
    };

    public bool Contains(string? voiceName) =>
        !string.IsNullOrWhiteSpace(voiceName) &&
        (MaleVoices.Contains(voiceName!, StringComparer.OrdinalIgnoreCase) ||
         FemaleVoices.Contains(voiceName!, StringComparer.OrdinalIgnoreCase));
}
