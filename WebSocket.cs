using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class WebSocketClient
{
    private ClientWebSocket _client;

    public async Task ConnectAsync(Uri uri)
    {
        _client = new ClientWebSocket(); // Создаем новый объект ClientWebSocket для установки соединения с сервером.
        await _client.ConnectAsync(uri, CancellationToken.None); // Вызываем метод ConnectAsync для подключения к серверу по указанному URI.
    }

    public async Task SendAsync(string message)
    {
        var encoded = Encoding.UTF8.GetBytes(message); // Кодируем сообщение в массив байтов с использованием кодировки UTF-8.
        var buffer = new ArraySegment<byte>(encoded, 0, encoded.Length); // Создаем массив байтов из закодированного сообщения.
        await _client.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None); // Отправляем сообщение на сервер.
    }

    public async Task<string> ReceiveAsync()
    {
        var buffer = new ArraySegment<byte>(new byte[1024]); // Создаем буфер для принятия данных от сервера.
        var result = await _client.ReceiveAsync(buffer, CancellationToken.None); // Принимаем данные от сервера.
        return Encoding.UTF8.GetString(buffer.Array, 0, result.Count); // Декодируем принятые данные и возвращаем текстовое сообщение.
    }
}
