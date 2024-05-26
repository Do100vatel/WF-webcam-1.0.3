using Fleck;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace VideChatApp
{
    public class WebSocketServer
    {
        private readonly Fleck.WebSocketServer _server;
        private readonly byte[] _encryptionKey;
        private readonly byte[] _encryptionIV;
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
                    // Расшифровать сообщение и вызвать событие MessageReceived
                    string decryptedMessage = DecryptMessage(message);
                    MessageReceived?.Invoke(this, decryptedMessage);
                };
            });

            _encryptionKey = GenerateRandomKey(32); // 256 бит
            _encryptionIV = GenerateRandomKey(16);  // 128 бит
        }

        public void SendMessage(string message)
        {
            // Шифровать сообщение перед отправкой
            string encryptedMessage = EncryptMessage(message);

            foreach (var socket in _activeSockets)
            {
                socket.Send(encryptedMessage);
            }
        }


        private string EncryptMessage(string message)
        {
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = _encryptionKey;
                aesAlg.IV = _encryptionIV;

                // Создание объекта шифратора для выполнения потокового шифрования данных
                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                // Cоздание потока для записи зашифрованных данных
                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            // Записать все данные в поток
                            swEncrypt.Write(message);
                        }
                        // Вернуть зашифрованные данные в виде массива байтов
                        return Convert.ToBase64String(msEncrypt.ToArray());
                    }
                }

            }
        }


        private string DecryptMessage(string encryptedMessage)
        {
            try
            {
                using (Aes aesAlg = Aes.Create())
                {
                    aesAlg.Key = _encryptionKey;
                    aesAlg.IV = _encryptionIV;

                    // Создание объекта дешифратора для выполнения потокового дешифрования данных
                    ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                    // Создание потока для чтения зашифрованных данных
                    using (MemoryStream msDecrypt = new MemoryStream(Convert.FromBase64String(encryptedMessage)))
                    {
                        using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                        {
                            using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                            {
                                // Чтение расшифрованных данных из потока
                                string decryptedText = srDecrypt.ReadToEnd();

                                // Вернуть расшифрованные данные
                                return decryptedText;
                            }
                        }
                    }
                }
            }
            catch (CryptographicException ex)
            {
                // Обработка ошибок криптографии
                Console.WriteLine("CryptographicException при дешифровании сообщения: " + ex.Message);
                return null; // Возвращаем null в случае ошибки
            }
            catch (Exception ex)
            {
                // Общая обработка ошибок
                Console.WriteLine("Произошла ошибка при дешифровании сообщения: " + ex.Message);
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
    }
}
    