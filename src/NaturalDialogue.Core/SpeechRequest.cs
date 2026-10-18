namespace NaturalDialogue.Core;

/// <summary>
/// A single line of dialogue that is ready to be handed to the speech synthesizer.
/// </summary>
public sealed class SpeechRequest : IEquatable<SpeechRequest>
{
    public SpeechRequest(string voiceName, string text, int volume, int rate)
    {
        VoiceName = voiceName ?? throw new ArgumentNullException(nameof(voiceName));
        Text = text ?? throw new ArgumentNullException(nameof(text));
        Volume = SpeechSettings.ClampVolume(volume);
        Rate = SpeechSettings.ClampRate(rate);
    }

    public string VoiceName { get; }

    public string Text { get; }

    public int Volume { get; }

    public int Rate { get; }

    public bool Equals(SpeechRequest? other) =>
        other is not null &&
        VoiceName == other.VoiceName &&
        Text == other.Text &&
        Volume == other.Volume &&
        Rate == other.Rate;

    public override bool Equals(object? obj) => Equals(obj as SpeechRequest);

    public override int GetHashCode() => HashCode.Combine(VoiceName, Text, Volume, Rate);

    public override string ToString() => $"{VoiceName}: \"{Text}\" (volume {Volume}, rate {Rate})";
}
