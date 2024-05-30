using Fleck;
using System.Collections.Generic;
using System;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.IO;
using System.Security.Cryptography;

namespace VideChatApp
{
    public class WebSocketServer
    {
        private readonly Fleck.WebSocketServer _server;
        private readonly byte[] _encryptionKey;
        private readonly List<IWebSocketConnection> _activeSockets; // Список активных сокетов

        public event EventHandler<string> MessageReceived;

        public WebSocketServer(string address)
        {
            _server = new Fleck.WebSocketServer(address);
            _activeSockets = new List<IWebSocketConnection>();

            _server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    Console.WriteLine($"Client connected: {socket.ConnectionInfo.ClientIpAddress}");
                    _activeSockets.Add(socket); // Добавление открытого сокета в список активных сокетов
                };
                socket.OnClose = () =>
                {
                    Console.WriteLine($"Client disconnected: {socket.ConnectionInfo.ClientIpAddress}");
                    _activeSockets.Remove(socket); // Удаление закрытого сокета из списка активных сокето
                };
                socket.OnMessage = message =>
                {
                    try
                    {
                        // Расшифровать сообщение и вызвать событие MessageReceived
                        string decryptedMessage = DecryptMessage(message);
                        if (decryptedMessage != null)
                        {
                            MessageReceived?.Invoke(this, decryptedMessage);
                        }
                    }
                    catch(Exception ex)
                    {
                        LogError("Error decrypting message", ex);
                    }
                };
            });

            _encryptionKey = GenerateRandomKey(32); // 256 бит
        }

        public void SendMessage(string message)
        {
            // Шифровать сообщение перед отправкой
            try
            {
                string encryptedMessage = EncryptMessage(message);

                foreach (var socket in _activeSockets)
                {
                    socket.Send(encryptedMessage);
                }
            }
            catch (Exception ex)
            {
                LogError("Error encrypting message", ex);
            }
        }


        private string EncryptMessage(string message)
        {
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = _encryptionKey;
                aesAlg.GenerateIV(); // енерация нового IV для каждого сообщения

                // Создание объекта шифратора для выполнения потокового шифрования данных
                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                // Cоздание потока для записи зашифрованных данных
                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    msEncrypt.Write(aesAlg.IV, 0, aesAlg.IV.Length); // Запись IV в начало потока

                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            // Записать все данные в поток
                            swEncrypt.Write(message);
                        }
                    }

                    // Вернуть зашифрованные данные в виде массива байтов
                    return Convert.ToBase64String(msEncrypt.ToArray());
                }

            }
        }


        private string DecryptMessage(string encryptedMessage)
        {
            try
            {
                byte[] fullCipher = Convert.FromBase64String(encryptedMessage);

                using (Aes aesAlg = Aes.Create())
                {
                    byte[] iv = new byte[16];
                    byte[] cupherText = new byte[fullCipher.Length - iv.Length];

                    Array.Copy(fullCipher, iv, iv.Length);
                    Array.Copy(fullCipher, iv.Length, cipherText, 0, cipherText.Length);

                    aesAlg.Key = _encryptionKey;
                    aesAlg.IV = iv;

                    // Создание объекта дешифратора для выполнения потокового дешифрования данных
                    ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                    // Создание потока для чтения зашифрованных данных
                    using (MemoryStream msDecrypt = new MemoryStream(cipherText))
                    {
                        using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                        {
                            using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                            {
                                return srDecrypt.ReadToEnd();
                            }
                        }
                    }
                }
            }
            catch (CryptographicException ex)
            {
                // Обработка ошибок криптографии
                LogError("Cryptographic error while decrypting message", ex);
                return null; // Возвращаем null в случае ошибки
            }
            catch (Exception ex)
            {
                // Общая обработка ошибок
                LogError("General error while decrypting message", ex);
                return null; // Возвращаем null в случае ошибки
            }
        }

        private byte[] GenerateRandomKey(int size)
        {
            byte[] key = new byte[size];
            using(var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(key);
            }
            return key;
        }

        private void LogError(string message, Exception ex)
        {
            Console.WriteLine($"{message}: {ex.Message}");
            File.AppendAllText("errors.log", $"{DateTime.Now} - {message}: {ex} \n")
        }
    }
}
    