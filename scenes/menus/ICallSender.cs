using MergedCells.Core;

namespace MergedCells;

// How a screen sends its own server calls without holding the queue: Main answers off when there's no server for the run
public interface ICallSender
{
    CallHandle<T> Send<T>(ServerCall<T> call)
        where T : class;
}
