using Emgu.CV;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace WF_webcam_1._0._0
{
    public partial class Form1 : Form
    {
        private VideoCapture _capture;
        private bool _initialSizeCalculated = false;
        private float _initialScale;
        private Point _initialCenter;
        private bool isDragging = false;
        private Point lastPoint;
        private bool isCameraRunning = true;

        private Button buttonToggleCamera;
        private Button buttonExit;
        private Button buttonAddParticipant;

        private TextBox textBoxMessage;
        private Button buttonSendMessage;
        private Button buttonStartCall;
        private Button buttonEndCall;

        private Image placeholderImage;

        private WebSocketClient _webSocketClient;

        public Form1()
        {
            InitializeComponent();
            this.SizeChanged += Form1_SizeChanged;
            pictureBox1.Anchor = AnchorStyles.None;

            pictureBox1.MouseDown += PictureBox1_MouseDown;
            pictureBox1.MouseMove += PictureBox1_MouseMove;
            pictureBox1.MouseUp += PictureBox1_MouseUp;

            // Добавление TextBox для ввода сообщений
            textBoxMessage = new TextBox
            {
                Location = new Point(10, this.ClientSize.Height - 100),
                Size = new Size(400, 30)
            };

            // Добавление Button для отправки сообщений
            buttonSendMessage = new Button
            {
                Text = "Отправить",
                Location = new Point(textBoxMessage.Right + 10, textBoxMessage.Top),
                Size = new Size(100, 30)
            };
            buttonSendMessage.Click += ButtonSendMessage_Click;

            // Добавление Button для начала звонка
            buttonStartCall = new Button
            {
                Text = "Начать звонок",
                Location = new Point(10, this.ClientSize.Height - 50),
                Size = new Size(120, 30)
            };
            buttonStartCall.Click += ButtonStartCall_Click;

            // Добавление Button для завершения звонка
            buttonEndCall = new Button
            {
                Text = "Завершить звонок",
                Location = new Point(buttonStartCall.Right + 10, buttonStartCall.Top),
                Size = new Size(120, 30)
            };
            buttonEndCall.Click += ButtonEndCall_Click;

            this.Controls.Add(textBoxMessage);
            this.Controls.Add(buttonSendMessage);
            this.Controls.Add(buttonStartCall);
            this.Controls.Add(buttonEndCall);

            InitializeControls();

            // Загрузка изображения заглушки (GIF)
            placeholderImage = Properties.Resources.Social_dino_with_hat;

            // Инициализация WebSocketClient
            _webSocketClient = new WebSocketClient(new Uri("wss://your-websocket-server.com"));
            _webSocketClient.MessageReceived += WebSocketClient_MessageReceived;

            // Подключение к WebSocket серверу при загрузке формы
            ConnectToWebSocketServer();
        }

            private void InitializeControls()
        {
            buttonToggleCamera = new Button
            {
                Text = "Turn off Camera",
                Location = new Point(10, this.ClientSize.Height - 50),
                Size = new Size(120, 30)
            };
            buttonToggleCamera.Click += ButtonToggleCamera_Click;

            buttonExit = new Button
            {
                Text = "Exit",
                Location = new Point(140, this.ClientSize.Height - 50),
                Size = new Size(120, 30)
            };
            buttonExit.Click += ButtonExit_Click;

            buttonAddParticipant = new Button
            {
                Text = "Добавить собеседника",
                Location = new Point(270, this.ClientSize.Height - 50),
                Size = new Size(150, 30)
            };

            this.Controls.Add(buttonToggleCamera);
            this.Controls.Add(buttonExit);
            this.Controls.Add(buttonAddParticipant);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            _capture = new VideoCapture(0);

            if (!_capture.IsOpened)
            {
                MessageBox.Show("Не удалось открыть камеру.");
                Close();
                return;
            }

            Application.Idle += ProcessFrame;
        }

        private void ProcessFrame(object sender, EventArgs e)
        {
            if (isDragging)
                return;

            using (Mat frame = new Mat())
            {
                _capture.Read(frame);  // Read a frame from the webcam

                if (!frame.IsEmpty)
                {
                    Bitmap bitmap = ConvertToBitmap(frame);  // Convert the frame to a Bitmap

                    if (!_initialSizeCalculated)
                    {
                        CalculateInitialSize(bitmap);  // Calculate the initial size and scale of the Bitmap
                        _initialSizeCalculated = true;
                    }

                    // Process the frame (e.g., apply filters, detect objects, etc.)
                    // Modify the bitmap here if needed

                    pictureBox1.Image = ScaleAndCenterBitmap(bitmap, _initialScale, _initialCenter);  // Scale and center the Bitmap in the PictureBox
                }
            }
    }

        private Bitmap ScaleAndCenterBitmap(Bitmap bitmap, float scale, Point center)
        {
            int pictureBoxWidth = pictureBox1.ClientSize.Width;
            int pictureBoxHeight = pictureBox1.ClientSize.Height;

            int displayWidth = (int)(bitmap.Width * scale);
            int displayHeight = (int)(bitmap.Height * scale);

            int x = (pictureBoxWidth - displayWidth) / 2;
            int y = (pictureBoxHeight - displayHeight) / 2;

            Bitmap scaledBitmap = new Bitmap(pictureBoxWidth, pictureBoxHeight);
            using (Graphics g = Graphics.FromImage(scaledBitmap))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(bitmap, new Rectangle(x, y, displayWidth, displayHeight));
            }

            return scaledBitmap;
        }

        private void CalculateInitialSize(Bitmap bitmap)
        {
            int pictureBoxWidth = pictureBox1.ClientSize.Width;
            int pictureBoxHeight = pictureBox1.ClientSize.Height;

            float ratio = Math.Min((float)pictureBoxWidth / bitmap.Width, (float)pictureBoxHeight / bitmap.Height);
            _initialScale = ratio;

            int displayWidth = (int)(bitmap.Width * ratio);
            int displayHeight = (int)(bitmap.Height * ratio);

            _initialCenter = new Point((pictureBoxWidth - displayWidth) / 2, (pictureBoxHeight - displayHeight) / 2);
        }

        private Bitmap ConvertToBitmap(Mat image)
        {
            Bitmap bitmap = new Bitmap(image.Width, image.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            System.Drawing.Imaging.BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, image.Width, image.Height), System.Drawing.Imaging.ImageLockMode.WriteOnly, bitmap.PixelFormat);
            IntPtr ptr = bmpData.Scan0;

            int bytes = Math.Abs(bmpData.Stride) * bitmap.Height;
            byte[] rgbValues = new byte[bytes];

            System.Runtime.InteropServices.Marshal.Copy(image.DataPointer, rgbValues, 0, bytes);
            System.Runtime.InteropServices.Marshal.Copy(rgbValues, 0, ptr, bytes);

            bitmap.UnlockBits(bmpData);

            return bitmap;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_capture != null && _capture.IsOpened)
            {
                _capture.Dispose();
            }
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
        }

        private void Form1_SizeChanged(object sender, EventArgs e)
        {
            int width = (int)(this.Width * 0.5); // 50% ширины окна
            int height = (int)(this.Height * 0.6); // 60% высоты окна
            pictureBox1.Size = new Size(width, height);

            if (!isDragging)
            {
                // Установка позиции pictureBox1 в левом верхнем углу
                pictureBox1.Location = new Point(0, 0);
            }

            buttonToggleCamera.Location = new Point(10, this.ClientSize.Height - 50);
            buttonExit.Location = new Point(140, this.ClientSize.Height - 50);
            buttonAddParticipant.Location = new Point(270, this.ClientSize.Height - 50);
        }

        private async void ConnectToWebSocketServer()
        {
            try
            {
                await _webSocketClient.ConnectAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка подключения к серверу WebSocket: {ex.Message}");
            }
        }

        private void WebSocketClient_MessageReceived(object sender, string message)
        {
            // Обработка входящих сообщений от WebSocket cервера 
            MessageBox.Show($"Сообщение от сервера: {message}");
        }




        private void PictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && !IsCursorOverButton(e.Location))
            {
                isDragging = true;
                lastPoint = e.Location;
                pictureBox1.Cursor = Cursors.Hand;
            }
        }

        private void PictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                int deltaX = e.Location.X - lastPoint.X;
                int deltaY = e.Location.Y - lastPoint.Y;

                int newLeft = pictureBox1.Left + deltaX;
                int newTop = pictureBox1.Top + deltaY;

                if (newLeft >= 0 && newLeft + pictureBox1.Width <= this.ClientSize.Width)
                {
                    pictureBox1.Left = newLeft;
                }
                if (newTop >= 0 && newTop + pictureBox1.Height <= this.ClientSize.Height - 50) // Предотвращение перетаскивания на кнопки
                {
                    pictureBox1.Top = newTop;
                }
            }
        }

        private void PictureBox1_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isDragging = false;
                pictureBox1.Cursor = Cursors.Default;
            }
        }

        private bool IsCursorOverButton(Point cursorLocation)
        {
            return cursorLocation.Y >= this.ClientSize.Height - 50; // Кнопки находятся в нижней части формы
        }

        private void ButtonToggleCamera_Click(object sender, EventArgs e)
        {
            if (isCameraRunning)
            {
                Application.Idle -= ProcessFrame;
                _capture.Pause();
                pictureBox1.Image = placeholderImage;
                buttonToggleCamera.Text = "Turn on camera";
            }
            else
            {
                Application.Idle += ProcessFrame;
                _capture.Start();
                pictureBox1.Image = null;
                buttonToggleCamera.Text = "Turn off camera";
            }
            isCameraRunning = !isCameraRunning;
        }

        private void ButtonExit_Click(object sender, EventArgs e)
        {
            Close();
        }
        private async void ButtonSendMessage_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(textBoxMessage.Text))
            {
                try
                {
                    await _webSocketClient.SendMessageAsync(textBoxMessage.Text);
                    textBoxMessage.Clear();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка отправки сообщения: {ex.Message}");
                }
            }
        }

        private async void ButtonStartCall_Click(object sender, EventArgs e)
        {
            try
            {
                await _webSocketClient.SendMessageAsync("Начало звонка");
                // Дополнительная логика для начала звонка
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при начале звонка: {ex.Message}");
            }
        }

        private async void ButtonEndCall_Click(object sender, EventArgs e)
        {
            try
            {
                await _webSocketClient.SendMessageAsync("Завершение звока");
                // Дополнительная логика для начала звонка
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при завершении звонка: {ex.Message}");
            }
        }
    }
}
