using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Socket0
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            displayMessage = new displayMessageDelegate(display);
        }

        Socket serviceSocket;
        Thread listenerThread;
        delegate void displayMessageDelegate(string message, string fileName);
        displayMessageDelegate displayMessage;
        private void startButton_Click(object sender, RoutedEventArgs e)
        {
            //启动服务端消息监听
            if (startButton.Content.ToString() == "启动")
            {
                startButton.Content = "停止";
                display("服务启动", null);
                //初始化socket
                serviceSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IPEndPoint ipe = new IPEndPoint(IPAddress.Parse(IPText.Text), Convert.ToInt32(PortText.Text));
                serviceSocket.Bind(ipe);
                serviceSocket.Listen(20);
                //启动一个监听线程
                listenerThread = new Thread(new ThreadStart(listenerStream));
                listenerThread.IsBackground = true;
                listenerThread.Start();
            }
            else if (startButton.Content.ToString() == "停止")
            {
                //终止监听
                serviceSocket.Disconnect(true);
                display("服务停止", null);
                listenerThread.Abort();
            }
        }
        Socket socketConnect;
        //监听并创建一个通信的线程
        private void listenerStream()
        {
            while (true)
            {
                socketConnect = serviceSocket.Accept();
                messagePanel.Dispatcher.Invoke(displayMessage, "连接成功", null);
                //创建通信线程  
                ParameterizedThreadStart pts = new ParameterizedThreadStart(ServerReceive);
                Thread th = new Thread(pts);
                th.IsBackground = true;
                th.Start(socketConnect);
            }
        }
        //接收消息
        private void ServerReceive(object o)
        {
            Socket connectSocket = o as Socket;
            byte[] buffer = new byte[1024];//初始化数据缓冲区
            int dataLength = 0;//接收数据的大小
            string receiveMessage = string.Empty; ;//文本消息
            long fileLength = 0;//文件的大小
            string filename = string.Empty;//文件的名字
            while (true)
            {
                //获取缓冲区的数据
                if (connectSocket != null)
                    dataLength = connectSocket.Receive(buffer);//把数据放到buffer中
                if (dataLength > 0)
                {
                    if (buffer[0] == 0)//传输内容为文本信息
                    {
                        receiveMessage = Encoding.Unicode.GetString(buffer, 1, dataLength - 1);
                        messagePanel.Dispatcher.Invoke(displayMessage, receiveMessage, null);
                    }
                    else if (buffer[0] == 1)//传输的内容为文件信息
                    {
                        fileLength = BitConverter.ToInt64(buffer, 1);//获取文件的长度
                        filename = Encoding.Unicode.GetString(buffer, 9, dataLength - 9);//获取文件的名字
                        //设置buffer用以接受文件
                        buffer = new byte[fileLength + 1];
                    }
                    else if (buffer[0] == 2)//开始接受文件
                    {
                        //将数据写入文件中
                        FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write);
                        fs.Write(buffer, 1, buffer.Length - 1);
                        fs.Flush();
                        fs.Close();
                        //还原buffer
                        buffer = new byte[1024];
                        messagePanel.Dispatcher.Invoke(displayMessage, null, filename);
                    }
                }
            }
        }
        /// <summary>
        /// 显示收到的消息
        /// </summary>
        /// <param name="message">接收到的文本消息</param>
        /// <param name="fileName">接收到的文件</param>
        private void display(string message, string fileName)
        {
            if (message != null)//将文本消息封装为TextBlock显示出来
            {
                TextBlock tb = new TextBlock();
                tb.TextWrapping = TextWrapping.Wrap;
                tb.Text = DateTime.Now.ToShortTimeString() + message;
                messagePanel.Children.Add(tb);
            }
            else if (fileName != null)//显示图片
            {
                Image im = new Image();
                im.Source = new BitmapImage(new Uri("F:\\WPF\\Socket0\\Socket0\\bin\\Debug\\" + fileName, UriKind.Absolute));
                im.Height = 100;
                im.Stretch = Stretch.Uniform;
                im.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                messagePanel.Children.Add(im);
            }
        }
        //发送消息
        private void serviceSendButton_Click(object sender, RoutedEventArgs e)
        {
            if (serviceSendTextBox.Text != null)
            {
                string message = "发送：“" + serviceSendTextBox.Text + "”";
                display(message, null);
                //组装这条消息
                byte[] byteArray = Encoding.Unicode.GetBytes(message);
                byte[] buffer = new byte[byteArray.Length + 1];
                Buffer.BlockCopy(byteArray, 0, buffer, 1, byteArray.Length);
                //发送
                socketConnect.Send(byteArray);
                //清空
                serviceSendTextBox.Text = string.Empty;
            }
        }
        //发送文件
        private void sendFileButton_Click(object sender, RoutedEventArgs e)
        {
            //选择要发送的图片
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "jpg|*.jpg";
            ofd.ValidateNames = true;
            ofd.CheckPathExists = true;
            ofd.CheckFileExists = true;
            ofd.Multiselect = false;
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                //获取选择的文件的信息
                string fileUri = ofd.FileName;//文件路径
                FileStream fs = new FileStream(fileUri, FileMode.Open, FileAccess.Read);
                string fileName = fileUri.Substring(fileUri.LastIndexOf("\\") + 1);//文件名
                long fileLength = fs.Length;//文件的大小
                //组装第一次要发送的文件信息
                byte[] byteLength = BitConverter.GetBytes(fileLength);//文件的大小
                byte[] byteFileName = Encoding.Unicode.GetBytes(fileName);//文件名
                byte[] buffer = new byte[9 + byteFileName.Length];
                buffer[0] = 1;//标志位
                Buffer.BlockCopy(byteLength, 0, buffer, 1, 8);
                Buffer.BlockCopy(byteFileName, 0, buffer, 9, byteFileName.Length);
                socketConnect.Send(buffer);
                //第二次，发送文件
                buffer = new byte[1 + fileLength];
                buffer[0] = 2;//标志位
                fs.Read(buffer, 1, (int)fileLength);//将文件写入缓冲区
                socketConnect.Send(buffer);
            }
        }
    }
}
