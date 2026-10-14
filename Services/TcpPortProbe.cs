using System.Net.Sockets;

namespace PostgresRedisManager.Services;

internal static class TcpPortProbe
{
    public static async Task<bool> IsOpenAsync(string host, int port, TimeSpan timeout)
    {
        using var client = new TcpClient();
        try
        {
            using var cancellation = new CancellationTokenSource(timeout);
            await client.ConnectAsync(host, port, cancellation.Token);
            return client.Connected;
        }
        catch (Exception exception) when (exception is SocketException or OperationCanceledException or ObjectDisposedException)
        {
            return false;
        }
    }
}
