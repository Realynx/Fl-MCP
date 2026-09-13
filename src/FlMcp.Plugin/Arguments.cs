using FlMcp.Protocol;

namespace FlMcp.Plugin;

internal static class Arguments
{
    public const int MaximumMixerTrack = 500;

    public static void Range(int value, int minimum, int maximum, string name)
    {
        if (value < minimum || value > maximum) throw new ArgumentOutOfRangeException(name, $"Expected {minimum}..{maximum}.");
    }

    public static void Range(double value, double minimum, double maximum, string name)
    {
        if (!double.IsFinite(value) || value < minimum || value > maximum)
            throw new ArgumentOutOfRangeException(name, $"Expected finite value {minimum}..{maximum}.");
    }

    public static string Name(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 256 || value.Any(char.IsControl)) throw new ArgumentException("Name must be 1..256 printable characters.");
        return value;
    }

    public static void Notes(NotesArgs args)
    {
        Range(args.Pattern, 1, 999, "pattern");
        if (args.Notes is null || args.Notes.Length is < 1 or > 2048)
            throw new ArgumentException("Supply 1..2048 notes per batch.");
        foreach (var note in args.Notes)
        {
            Range(note.Channel, 0, 999, "channel");
            Range(note.Key, 0, 127, "key");
            Range(note.StartTick, 0, int.MaxValue - 1, "startTick");
            Range(note.LengthTick, 1, int.MaxValue - note.StartTick, "lengthTick");
            Range(note.Velocity, 1, 127, "velocity");
        }
    }
}
