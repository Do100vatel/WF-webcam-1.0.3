using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VideChatApp
{
    public class WebSocketClient
    {
        private readonly ClientWebSocket _client;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Uri _uri;

        public event EventHandler<string> MessageReceived;

        public WebSocketClient(Uri uri)
        {
            _client = new ClientWebSocket();
            _cancellationTokenSource = new CancellationTokenSource();
            _uri = uri;
        }

        public async Task ConnectAsync()
        {
            await _client.ConnectAsync(_uri, _cancellationTokenSource.Token);
            Console.WriteLine("Connected to server.");
            await ReceiveLoopAsync();
        }

        public async Task SendMessageAsync(string message)
        {
            var buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(message));
            await _client.SendAsync(buffer, WebSocketMessageType.Text, true, _cancellationTokenSource.Token);
        }

        private async Task ReceiveLoopAsync()
        {
            var buffer = new byte[1024];
            while (_client.State == WebSocketState.Open)
            {
                try
                {
                    var result = await _client.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        MessageReceived?.Invoke(this, message);
                    }
                }
                catch (WebSocketException ex)
                {
                    Console.WriteLine($"WebSocketException during message reception: {ex.Message}");
                }
                catch (OperationCanceledException)
                {
                    // Connection closed gracefully
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during message reception: {ex.Message}");
                }
            }
        }

        public async Task DisconnectAsync()
        {
            if (_client.State == WebSocketState.Open || _client.State == WebSocketState.Connecting)
            {
                _cancellationTokenSource.Cancel();
                await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed by client.", CancellationToken.None);
                _cancellationTokenSource.Dispose();
                _client.Dispose();
            }
        }
    }
}
