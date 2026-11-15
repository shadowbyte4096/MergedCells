using System.Globalization;

namespace MergedCells.Curation;

// A command's words: the positional ones in order, then "--option value" pairs from the options it takes
internal sealed class Arguments
{
    private readonly Dictionary<string, string> options;

    private Arguments(IReadOnlyList<string> positional, Dictionary<string, string> options)
    {
        Positional = positional;
        this.options = options;
    }

    public IReadOnlyList<string> Positional { get; }

    // Throws UsageException for an option the command doesn't take, one given twice, or one with no value
    public static Arguments Parse(IReadOnlyList<string> args, params string[] known)
    {
        var positional = new List<string>();
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Count; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                positional.Add(args[i]);
                continue;
            }
            if (!known.Contains(args[i]))
            {
                throw new UsageException($"{args[i]} isn't an option of this command.");
            }
            if (i + 1 >= args.Count)
            {
                throw new UsageException($"{args[i]} needs a value.");
            }
            if (!options.TryAdd(args[i], args[i + 1]))
            {
                throw new UsageException($"{args[i]} is given twice.");
            }
            i++;
        }
        return new Arguments(positional, options);
    }

    public string? Option(string name) => options.GetValueOrDefault(name);

    public static int Whole(string text, string name, int least) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= least
            ? value
            : throw new UsageException($"{name} must be a whole number of at least {least}; \"{text}\" isn't.");

    public static ulong Seed(string text) =>
        ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new UsageException($"--seed must be a whole number of at least 0; \"{text}\" isn't.");
}

// A mistake in how a command was typed; the command prints it with its usage
internal sealed class UsageException(string message) : Exception(message);
