using System;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// The online level library (see docs/systems/custom-levels.md and server.md): its question, asked on the custom levels
// list when the player taps Library or Publish; publishing and removing from a level's sheet; the library screen; and a
// library level's box on the results card. Every request goes through the server queue, which checks the answer again at
// the moment of sending
public partial class Main
{
    // The library's order and filters, kept for the run
    private LibraryQuery libraryQuery = new(LibraryOrder.Newest);

    // What a yes to the library's question goes on to do: open the library, or publish
    private Action libraryThen;

    private static bool LibraryGiven => Consents.IsGiven(SaveGameFile.Current.ConsentTo(ConsentKind.Library), ConsentWordings.Library);

    private static bool ServerOn => server is not null && !server.IsStopped;

    private sealed class QueueSender : ICallSender
    {
        public CallHandle<T> Send<T>(ServerCall<T> call)
            where T : class => SendCall(call);
    }

    private void WireLibrary(CustomLevelsScreen list)
    {
        list.Consent.AnswerGuardSeconds = GameConfigFile.Current.Privacy.AskGuardSeconds;
        list.Consent.Answered += (wording, yes) =>
        {
            Answer(wording, yes);
            var then = libraryThen;
            libraryThen = null;
            if (yes)
            {
                then?.Invoke();
            }
        };
        list.Consent.Dismissed += () => libraryThen = null;
        list.LibraryPressed += () => WithLibrary(list, ShowLibrary);
        list.PublishPressed += (custom, status) => WithLibrary(list, () => Publish(list, custom, status));
        list.RemovePressed += custom =>
        {
            GD.Print($"Library: removing {custom.Name}");
            var call = SendCall(LibraryCalls.Remove(custom.Id));
            list.Watch("Removing…", () => LibraryLines.Remove(call));
        };
        list.LibraryChanged += ShowCustomLevels;
    }

    // Goes on at once when the library is given or the server is off for the run (the library then says it isn't
    // available, with nothing to ask about); otherwise asks, and a yes goes on. A no is asked again at the next tap: the
    // player asked
    private void WithLibrary(CustomLevelsScreen list, Action then)
    {
        if (LibraryGiven || !ServerOn)
        {
            then();
            return;
        }
        libraryThen = then;
        var changed = SaveGameFile.Current.ConsentTo(ConsentKind.Library) is { Yes: true };
        GD.Print($"Consent: asking about Library{(changed ? ", its wording changed" : "")}");
        list.Consent.Ask(ConsentWordings.Library, changed);
    }

    private static PublishState? PublishStateOf(CustomLevel custom, LevelStatus status) =>
        LibraryPublishing.StateOf(custom, status, SaveGameFile.Current, ServerOn);

    private static void Publish(CustomLevelsScreen list, CustomLevel custom, LevelStatus status)
    {
        GD.Print($"Library: publishing {custom.Name}");
        var call = SendCall(LibraryCalls.Publish(custom, status.Par!.Value, status.Score!.Value));
        list.Watch("Publishing…", () => LibraryLines.Publish(call));
    }

    public void ShowLibrary() => Swap(() =>
    {
        var library = Open<LibraryScreen>(LibraryScene);
        library.BackPressed += ShowCustomLevels;
        library.QueryChanged += query => libraryQuery = query;
        library.GetPressed += entry => library.ShowImport(GetFromLibrary(entry.Level));
        library.PlayPressed += custom => PlayCustom(custom, ShowLibrary, record: true);
        library.Open(
            new QueueSender(),
            libraryQuery,
            GameConfigFile.Current.CustomLevels,
            GameConfigFile.Current.Library,
            new DifficultyRater(GameConfigFile.Current.Difficulty),
            CustomLevelFiles.LoadAll,
            ServerOn);
        back = () =>
        {
            if (!library.LeaveYours())
            {
                ShowCustomLevels();
            }
        };
    });

    // Imports a level from the library as Import does a code, and writes it when the import brings one
    private static ImportResult GetFromLibrary(CustomLevel level)
    {
        var result = LevelImport.Of(
            level, CustomLevelFiles.LoadAll(), SaveGameFile.Current.SharedLevels, GameConfigFile.Current.CustomLevels, DateTimeOffset.UtcNow);
        if (result.Level is not null)
        {
            CustomLevelFiles.Write(result.Level);
            if (CustomLevelFiles.Notice is not null)
            {
                result = result.NotSaved();
            }
        }
        GD.Print($"Library: got {level.Name} ({result.Outcome})");
        return result;
    }

    // The results card's box for an imported level while the library is given: the finish is sent as the card appears,
    // and its answer carries the counts. Null hides the box (the player's own level, no yes, or no server)
    private static Func<LibraryCardSource> LibraryCardFor(CustomLevel custom)
    {
        if (!custom.IsImported)
        {
            return null;
        }
        return () =>
        {
            if (!LibraryGiven || !ServerOn)
            {
                return null;
            }
            GD.Print($"Library: finished {custom.Name}");
            return new LibraryCardSource(
                SendCall(LibraryCalls.Finish(custom.Id)),
                liked => SendCall(LibraryCalls.Like(custom.Id, liked)));
        };
    }
}
