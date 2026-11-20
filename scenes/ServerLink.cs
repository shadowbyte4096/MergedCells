using System.Text;
using Godot;
using MergedCells.Core;

namespace MergedCells;

// Sends the server queue's requests one at a time through Godot's own HTTP code, not .NET's (see "The .NET version" in
// docs/systems/build-and-platforms.md), and hands each answer back
public partial class ServerLink : Node
{
    private ServerQueue queue;
    private ServerConfig config;
    private HttpRequest http;
    private bool busy;

    public void Start(ServerQueue serverQueue, ServerConfig serverConfig)
    {
        queue = serverQueue;
        config = serverConfig;
        // Threads, so a TLS handshake never costs a frame
        http = new HttpRequest { UseThreads = true, Timeout = serverConfig.TimeoutSeconds };
        AddChild(http);
        http.RequestCompleted += OnCompleted;
    }

    public override void _Process(double delta)
    {
        if (queue is null || busy || queue.Next() is not { } request)
        {
            return;
        }
        busy = true;
        var error = http.Request(request.Url(config.Url), request.Headers(config.PublishableKey), HttpClient.Method.Post, request.Body);
        if (error != Error.Ok)
        {
            GD.PushWarning($"Server: a request couldn't start ({error})");
            busy = false;
            queue.Reply(new ServerReply(0, ""));
        }
    }

    private void OnCompleted(long result, long responseCode, string[] headers, byte[] body)
    {
        busy = false;
        var status = result == (long)HttpRequest.Result.Success ? (int)responseCode : 0;
        queue.Reply(new ServerReply(status, Encoding.UTF8.GetString(body)));
    }
}
