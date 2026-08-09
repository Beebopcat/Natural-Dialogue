using NaturalDialogue.Core;
using Xunit;

namespace NaturalDialogue.Core.Tests;

public class SpeechRequestTests
{
    [Fact]
    public void Constructor_clamps_volume_and_rate()
    {
        var request = new SpeechRequest("Ryan", "Hello", 500, -500);

        Assert.Equal(SpeechSettings.MaxVolume, request.Volume);
        Assert.Equal(SpeechSettings.MinRate, request.Rate);
    }

    [Fact]
    public void Constructor_rejects_null_voice_or_text()
    {
        Assert.Throws<ArgumentNullException>(() => new SpeechRequest(null!, "Hello", 50, 0));
        Assert.Throws<ArgumentNullException>(() => new SpeechRequest("Ryan", null!, 50, 0));
    }

    [Fact]
    public void Equality_compares_every_component()
    {
        var request = new SpeechRequest("Ryan", "Hello", 50, 1);

        Assert.Equal(request, new SpeechRequest("Ryan", "Hello", 50, 1));
        Assert.Equal(request.GetHashCode(), new SpeechRequest("Ryan", "Hello", 50, 1).GetHashCode());
        Assert.NotEqual(request, new SpeechRequest("Sonia", "Hello", 50, 1));
        Assert.NotEqual(request, new SpeechRequest("Ryan", "Goodbye", 50, 1));
        Assert.NotEqual(request, new SpeechRequest("Ryan", "Hello", 51, 1));
        Assert.NotEqual(request, new SpeechRequest("Ryan", "Hello", 50, 2));
    }

    [Fact]
    public void Equality_handles_null_and_other_types()
    {
        var request = new SpeechRequest("Ryan", "Hello", 50, 1);

        Assert.False(request.Equals(null));
        Assert.False(request.Equals((object?)"Ryan"));
        Assert.True(request.Equals((object)new SpeechRequest("Ryan", "Hello", 50, 1)));
    }

    [Fact]
    public void ToString_describes_the_request()
    {
        var request = new SpeechRequest("Ryan", "Hello", 50, 1);

        Assert.Equal("Ryan: \"Hello\" (volume 50, rate 1)", request.ToString());
    }
}
