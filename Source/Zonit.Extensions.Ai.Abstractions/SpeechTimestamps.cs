using Zonit.Extensions;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Synthesized audio together with per-character timing alignment — everything you need to
/// sync subtitles/captions or karaoke highlighting to a voice-over.
/// </summary>
/// <remarks>
/// Providers that don't return alignment throw from the timestamps call; ElevenLabs returns
/// character-level timings which you can roll up to words via <see cref="ToWords"/>.
/// </remarks>
public sealed class SpeechTimestamps
{
    /// <summary>The generated audio.</summary>
    public required Asset Audio { get; init; }

    /// <summary>Character-level alignment, in spoken order.</summary>
    public required IReadOnlyList<CharacterTiming> Characters { get; init; }

    /// <summary>
    /// Groups the character timings into word timings (splitting on whitespace). Each word spans
    /// from its first character's start to its last character's end — handy for subtitle cues.
    /// </summary>
    public IReadOnlyList<WordTiming> ToWords()
    {
        var words = new List<WordTiming>();
        var current = new System.Text.StringBuilder();
        TimeSpan start = default, end = default;

        void Flush()
        {
            if (current.Length == 0)
                return;
            words.Add(new WordTiming(current.ToString(), start, end));
            current.Clear();
        }

        foreach (var c in Characters)
        {
            if (string.IsNullOrWhiteSpace(c.Character))
            {
                Flush();
                continue;
            }

            if (current.Length == 0)
                start = c.Start;
            current.Append(c.Character);
            end = c.End;
        }
        Flush();

        return words;
    }
}

/// <summary>Timing of a single character within the synthesized audio.</summary>
/// <param name="Character">The character (as spoken).</param>
/// <param name="Start">Offset from the start of the audio where the character begins.</param>
/// <param name="End">Offset from the start of the audio where the character ends.</param>
public readonly record struct CharacterTiming(string Character, TimeSpan Start, TimeSpan End);

/// <summary>Timing of a whole word, rolled up from its characters (see <see cref="SpeechTimestamps.ToWords"/>).</summary>
/// <param name="Word">The word text.</param>
/// <param name="Start">Offset from the start of the audio where the word begins.</param>
/// <param name="End">Offset from the start of the audio where the word ends.</param>
public readonly record struct WordTiming(string Word, TimeSpan Start, TimeSpan End);
