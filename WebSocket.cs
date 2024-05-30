using System;
using System.Net.WebSockets;
using System.Text;
using System.Security.Cryptography;
using System.Net.Sockets;
public class WebSocketClient
{
    private TcpClient client;
    private NetworkStream stream;

    public WebSocket(string server, int port)
    {
        client = new TcpClient(server, port);
        stream = client.GetStream();
    }

    public void Send(string message)
    {
        try
        {
            // Генерация нового IV для каждого сообщения
            byte[] iv = new byte[16];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(iv);
            }

            // Запись IV в начало сообщения
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            byte[] dataToSend = new byte[iv.Length + messageBytes.Length];
            Array.Copy(iv, dataToSend, iv.Length);
            Array.Copy(messageBytes, 0, dataToSend, iv.Length, messageBytes.Length);

            // Шифрование сообщения с использованием IV
            using (var aes = new AesManaged())
            {
                aes.KeySize = 256;
                aes.GenerateKey();
                aes.GenerateIV();
                aes.Padding = PaddingMode.PKCS7;
                aes.Mode = CipherMode.CBC;
                aes.IV = iv;

                using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        cs.Write(dataToSend, 0, dataToSend.Length);
                        cs.FlushFinalBlock();
                    }
                    dataToSend = ms.ToArray();
                }
            }

            // Отправка зашифрованного сообщения 
            stream.Write(dataToSend, 0, dataToSend.Length);
        }
        catch (Exception ex)
        {
            // Логирование ошибок отправки
            LogError("Error sending message: " + ex.Message);
        }
    }

    public string Receive()
    {
        byte[] buffer = new byte[client.ReceiveBufferSize];
        int bytesRead = 0;
        try
        {
            bytesRead = stream.Read(buffer, 0, buffer.Length);
        }
        catch (Exception ex)
        {
            // Логирование ошибок приема
            LogError("Error receiving message: " + ex.Message);
        }

        if(bytesRead > 0)
        {
            // Дешифрование и обработка полученного сообщения
            try
            {
                // Получение IV из сообщения
                byte[] iv = new byte[16];
                Array.Copy(buffer, iv, iv.Length);

                // Дешифрование оставшейся части сообщения
                using (var aes = new AesManaged())
                {
                    aes.KeySize = 256;
                    aes.GenerateKey();
                    aes.Padding = PaddingMode.PKCS7;
                    aes.Mode = CipherMode.CBC;
                    aes.IV = iv;

                    using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                    using (var ms = new MemoryStream())
                    {
                        using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Write))
                        {
                            cs.Write(buffer, iv.Length, bytesRead - iv.Length);
                            cs.FlushFinalBlock();
                        }
                        return Encoding.UTF8.GetString(ms.ToArray());
                    }
                }            
            }
            catch (Exception ex)
            {
                // Логирование ошибок дешифрования
                LogError("Error decrypting message: " + ex.Message);
            }
        }
        return null;
    }

    private void LogError(string errorMessage)
    {
        // Логирование ошибок в файл errors.log
        using (StreamWriter writer = File.AppendText("errors.log"))
        {
            writer.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " - " + errorMessage);
        }
    }
}
