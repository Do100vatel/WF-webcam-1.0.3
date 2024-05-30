using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace VideChatApp
{
    public class WebSocketClient
    {
        private readonly ClientWebSocket _client;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Uri _uri;
        private readonly byte[] _aesKey;
        private readonly byte[] _aesIV;

        public event EventHandler<string> MessageReceived;

        public WebSocketClient(Uri uri, byte[] aesKey, byte[] aesIV)
        {
            _client = new ClientWebSocket();
            _cancellationTokenSource = new CancellationTokenSource();
            _uri = uri;
            _aesKey = aesKey;
            _aesIV = aesIV;
        }

        public async Task ConnectAsync()
        {
            await _client.ConnectAsync(_uri, _cancellationTokenSource.Token);
            Console.WriteLine("Connected to server.");
            await ReceiveLoopAsync();
        }

        public async Task SendMessageAsync(string message)
        {
            try
            {
                byte[] encryptedMessage = EncryptMessage(message, _aesKey, _aesIV);
                await _client.SendAsync(new ArraySegment<byte>(encryptedMessage), WebSocketMessageType.Binary, true, _cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending message: {ex.Message}");
            }
        }

        private async Task ReceiveLoopAsync()
        {
            var buffer = new byte[1024];
            while (_client.State == WebSocketState.Open)
            {
                try
                {
                    var result = await _client.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);
                    if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        string decryptedMessage = DecryptMessage(buffer, _aesKey, _aesIV);
                        MessageReceived?.Invoke(this, decryptedMessage);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error receiving message: {ex.Message}");
                }
            }
        }

        private byte[] EncryptMessage(string message, byte[] key, byte[] iv)
        {
            using (var aes = new AesManaged())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Padding = PaddingMode.PKCS7;
                aes.Mode = CipherMode.CBC;

                using (var encryptor = aes.CreateEncryptor())
                using (var memoryStream = new MemoryStream())
                {
                    using (var crypto = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                    {
                        byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                        crypto.Write(messageBytes, 0, messageBytes.Length);
                        crypto.FlushFinalBlock();
                        return memoryStream.ToArray();
                    }
                }
            }
        }

        private string DecryptMessage(byte[] encryptedMessage, byte[] key, byte[] iv)
        {
            using (var aes = new AesManaged())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Padding = PaddingMode.PKCS7;
                aes.Mode = CipherMode.CBC;

                using (var decryptor = aes.CreateDecryptor())
                using (var memoryStream = new MemoryStream(encryptedMessage))
                using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                using (var streamReader = new StreamReader(cryptoStream))
                {
                    return streamReader.ReadToEnd();
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
