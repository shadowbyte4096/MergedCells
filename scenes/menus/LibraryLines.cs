using MergedCells.Core;

namespace MergedCells;

// The sentence each library call ends with on its sheet, or null while it waits
public static class LibraryLines
{
    public const string Unreachable = "Couldn't reach the library. Try again later.";

    public static string Publish(CallHandle<PublishReply> call) => call.State switch
    {
        CallState.Waiting => null,
        CallState.Answered => call.Answer!.Outcome switch
        {
            PublishOutcome.Published => "Published. Everyone can find it in the library.",
            PublishOutcome.Taken => "This level's ID is taken in the library.",
            PublishOutcome.Copy => "A level like this is already in the library. Change it to publish it.",
            PublishOutcome.Campaign => "This is a campaign level. Change it to publish it.",
            PublishOutcome.Removed => "This level was removed from the library.",
            PublishOutcome.Stopped => "You can't publish levels any more.",
            PublishOutcome.Full => "You've published as many levels as the library takes. Remove one first.",
            _ => Unreachable,
        },
        _ => Unreachable,
    };

    public static string Remove(CallHandle<RemoveReply> call) => call.State switch
    {
        CallState.Waiting => null,
        CallState.Answered => "Removed from the library.",
        _ => Unreachable,
    };

    public static string Report(CallHandle<ReportReply> call) => call.State switch
    {
        CallState.Waiting => null,
        CallState.Answered => "Thanks. The developer will look at it.",
        _ => Unreachable,
    };

    public static string Block(CallHandle<BlockReply> call) => call.State switch
    {
        CallState.Waiting => null,
        CallState.Answered => "You won't see any levels by whoever made this one. They aren't told.",
        _ => Unreachable,
    };
}
