using ComTool.LuaEnv;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Animation;

namespace ComTool
{
    class Uart
    {
        //废弃的串口对象，存放处，尝试fix[System.ObjectDisposedException: 已关闭 Safe handle]
        //https://drdump.com/Problem.aspx?ProblemID=524533
        private List<SerialPort> useless = new List<SerialPort>();

        public SerialPort serial = new SerialPort();
        public event EventHandler UartDataRecived;
        public event EventHandler UartDataSent;
        private Stream lastPortBaseStream = null;
        private bool _rts = false;
        private bool _dtr = true;
        private int SentCount = 0;
        //收到串口事件的信号量
        public EventWaitHandle WaitUartReceive = new AutoResetEvent(true);
        //收到串口事件的信号量
        //private EventWaitHandle WaitUart1Receive = new AutoResetEvent(true);


        public bool Rts
        {
            get
            {
                return _rts;
            }
            set
            {
                serial.RtsEnable = _rts = value;
            }
        }
        public bool Dtr
        {
            get
            {
                return _dtr;
            }
            set
            {
                serial.DtrEnable = _dtr = value;
            }
        }

        private readonly object objLock = new object();
        private string UartId = "";
        private int ReceivedCount = 0;

        /// <summary>
        /// 初始化串口各个触发函数
        /// </summary>
        public Uart(int uartId)
        {
            if(uartId != -1)
            {
                UartId = uartId.ToString();
            }

            //声明接收到事件
            serial.DataReceived += Serial_DataReceived;
            serial.RtsEnable = Rts;
            serial.DtrEnable = Dtr;
            new Thread(ReadData).Start();
            //适配一下通用通道
            LuaApis.SendChannelsRegister("uart" + UartId, (data, _) =>
            {
                if (IsOpen() && data != null)
                {
                    SendData(data);
                    return true;
                }
                else
                    return false;
            });

        }

        /// <summary>
        /// 刷新串口对象
        /// </summary>
        private void refreshSerialDevice()
        {            
            //serial = new SerialPort();
            ////声明接收到事件
            //serial.DataReceived += Serial_DataReceived;
            //serial.BaudRate = Global.baudRate;
            //serial.Parity = (Parity)Global.parity;
            //serial.DataBits = Global.dataBits;
            //serial.StopBits = (StopBits)Global.stopBit;
            //serial.RtsEnable = Rts;
            //serial.DtrEnable = Dtr;

        }

        /// <summary>
        /// 获取串口设备COM名
        /// </summary>
        /// <returns></returns>
        public string GetName()
        {
            return serial.PortName;
        }

        /// <summary>
        /// 设置串口设备COM名
        /// </summary>
        /// <returns></returns>
        public void SetName(string s)
        {
            serial.PortName = s;
        }

        /// <summary>
        /// 查看串口打开状态
        /// </summary>
        /// <returns></returns>
        public bool IsOpen()
        {
            return serial.IsOpen;
        }

        /// <summary>
        /// 开启串口
        /// </summary>
        public void Open()
        {
            string temp = serial.PortName;
            refreshSerialDevice();
            serial.PortName = temp;
            serial.Open();
            //lastPortBaseStream = serial.BaseStream;
        }

        /// <summary>
        /// 关闭串口
        /// </summary>
        public void Close()
        {
            refreshSerialDevice();
            serial.Close();
        }

        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="data">数据内容</param>
        public void SendData(byte[] data)
        {
            if (data.Length == 0)
                return;
            serial.Write(data, 0, data.Length);
            SentCount += data.Length;
            UartDataSent(data, EventArgs.Empty);//回调
        }


        //接收到事件
        private void Serial_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            //if(UartId == "0")
            //{
            //    Global.WaitUart0Receive.Set();
            //}
            //else if(UartId == "1")
            //{
            //    Global.WaitUart1Receive.Set();
            //}
            WaitUartReceive.Set();
        }

        /// <summary>
        /// 单独开个线程接收数据
        /// </summary>
        private void ReadData()
        {
            //if (UartId == "0")
            //{
            //    Global.WaitUart0Receive.Reset();
            //}
            //else if (UartId == "1")
            //{
            //    Global.WaitUart1Receive.Reset();
            //}
            WaitUartReceive.Reset();
            while (true)
            {
                //if (UartId == "0")
                //{
                //    Global.WaitUart0Receive.WaitOne();
                //}
                //else if (UartId == "1")
                //{
                //    Global.WaitUart1Receive.WaitOne();
                //}
                WaitUartReceive.WaitOne();
                if (Global.isMainWindowsClosed)
                    return;
                if (Global.timeout > 0)
                    System.Threading.Thread.Sleep(Global.timeout);//等待时间
                List<byte> result = new List<byte>();
                while (true)//循环读
                {
                    if (serial == null || !serial.IsOpen)//串口被关了，不读了
                        break;
                    try
                    {
                        int length = serial.BytesToRead;
                        if (length == 0)//没数据，退出去
                            break;
                        byte[] rev = new byte[length];
                        serial.Read(rev, 0, length);//读数据
                        if (rev.Length == 0)
                            break;
                        result.AddRange(rev);//加到list末尾
                    }
                    catch { break; }//崩了？

                    if (result.Count > Global.maxLength)//长度超了
                        break;
                    if (Global.bitDelay && Global.timeout > 0)//如果是设置了等待间隔时间
                    {
                        System.Threading.Thread.Sleep(Global.timeout);//等待时间
                    }
                }
                ReceivedCount += result.Count;


                if (result.Count > 0)
                    try
                    {
                        var r = result.ToArray();
                        UartDataRecived(r, EventArgs.Empty);//回调事件
                        LuaApis.SendChannelsReceived("uart" + UartId, r);
                    }
                    catch { }
            }
        }
    }
}
