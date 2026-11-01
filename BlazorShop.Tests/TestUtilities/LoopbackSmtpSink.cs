namespace BlazorShop.Tests.TestUtilities;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using MimeKit;

internal enum SmtpTestMode { Healthy, ClosedPort, LostAcknowledgement, HoldAcknowledgement, EchoPayloadRejection }

// Disposable loopback receiver. Never writes MIME bodies, links or tokens to output/files.
internal sealed class LoopbackSmtpSink : IAsyncDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource cancellation = new();
    private readonly ConcurrentBag<TcpClient> clients = [];
    private readonly ConcurrentBag<Task> handlers = [];
    private readonly Task loop;
    private int mode;
    public int Port { get; }
    public ConcurrentQueue<string> Messages { get; } = new();
    public TaskCompletionSource DataReceived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ReleaseAcknowledgement { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public SmtpTestMode Mode { get => (SmtpTestMode)Volatile.Read(ref mode); set => Volatile.Write(ref mode, (int)value); }

    public LoopbackSmtpSink(SmtpTestMode mode = SmtpTestMode.Healthy)
    {
        Mode = mode;
        listener.Start();
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        if (mode == SmtpTestMode.ClosedPort) { listener.Stop(); loop = Task.CompletedTask; }
        else loop = RunAsync();
    }

    public Uri ConfirmationLink(int index = 0)
    {
        using var content = new MemoryStream(Encoding.UTF8.GetBytes(Messages.ToArray()[index]));
        using var mime = MimeMessage.Load(content);
        var href = Regex.Match(mime.HtmlBody!, "href=\"([^\"]+)\"").Groups[1].Value;
        return new Uri(WebUtility.HtmlDecode(href));
    }

    private async Task RunAsync()
    {
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(cancellation.Token);
                clients.Add(client);
                handlers.Add(HandleAsync(client));
            }
        }
        catch (OperationCanceledException) { }
        catch (SocketException) when (cancellation.IsCancellationRequested) { }
    }

    private async Task HandleAsync(TcpClient client)
    {
        try
        {
            using (client)
            await using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true))
            await using (var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true, NewLine = "\r\n" })
            {
                await writer.WriteLineAsync("220 localhost isolated SMTP");
                while (await reader.ReadLineAsync(cancellation.Token) is { } line)
                {
                    if (line.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase)) await writer.WriteLineAsync("250-localhost\r\n250 SIZE 1000000");
                    else if (line.StartsWith("DATA", StringComparison.OrdinalIgnoreCase))
                    {
                        await writer.WriteLineAsync("354 End with dot");
                        var message = new StringBuilder();
                        while (await reader.ReadLineAsync(cancellation.Token) is { } data && data != ".") message.AppendLine(data);
                        Messages.Enqueue(message.ToString());
                        DataReceived.TrySetResult();
                        var currentMode = Mode;
                        if (currentMode == SmtpTestMode.LostAcknowledgement) break;
                        if (currentMode == SmtpTestMode.HoldAcknowledgement) await ReleaseAcknowledgement.Task.WaitAsync(cancellation.Token);
                        if (currentMode == SmtpTestMode.EchoPayloadRejection)
                        {
                            var token = QueryHelpers.ParseQuery(ConfirmationLink(Messages.Count - 1).Query)["token"];
                            await writer.WriteLineAsync($"550 synthetic-rejection {token}");
                        }
                        else await writer.WriteLineAsync("250 accepted");
                    }
                    else if (line.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase)) { await writer.WriteLineAsync("221 bye"); break; }
                    else await writer.WriteLineAsync("250 OK");
                }
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (IOException) { } // SMTP clients can disconnect after a failed/ambiguous send.
        catch (SocketException) { }
        catch (ObjectDisposedException) when (cancellation.IsCancellationRequested) { }
    }

    public async ValueTask DisposeAsync()
    {
        cancellation.Cancel();
        listener.Stop();
        foreach (var client in clients) client.Dispose();
        await loop;
        await Task.WhenAll(handlers);
        cancellation.Dispose();
    }
}
