using ComTool.Properties;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Media;

namespace ComTool
{
    class Global
    {
        public static event EventHandler ProgramClosedEvent;
        public const int MajorVersion = 0;
        public const int MinorVersion = 1;
        public const int PatchVersion = 3;
        //配置文件路径（普通exe时，会被替换为AppPath）
        //public static string ProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\llcom\";
        public static string ProfilePath = Environment.CurrentDirectory + "\\";


        public static Color SendColor = Colors.Green;
        public static Color SendColorDark = Colors.DarkGreen;
        public static Color ReceiveColor = Colors.Red;
        public static Color ReceiveColorDark = Colors.IndianRed;
        public static Color TimeColor = Colors.Gray;
        public static Color LogColor = Colors.DarkBlue;

        //主窗口是否被关闭？
        private static bool _isMainWindowsClosed = false;
        public static bool isMainWindowsClosed
        {
            get
            {
                return _isMainWindowsClosed;
            }
            set
            {
                _isMainWindowsClosed = value;
                if (value)
                {
                    //uart.WaitUartReceive.Set();
                    uart0.WaitUartReceive.Set();
                    uart1.WaitUartReceive.Set();
                    //Global.WaitUart0Receive.Set();
                    //Global.WaitUart1Receive.Set();
                    //Logger.CloseUartLog();
                    //Logger.CloseLuaLog();
                    //if (File.Exists(ProfilePath + "lock"))
                    //    File.Delete(ProfilePath + "lock");
                    ProgramClosedEvent?.Invoke(null, EventArgs.Empty);
                }
            }
        }

        //给全局使用的设置参数项
        //public static Settings setting;
        //public static Uart uart = new Uart(-1);
        public static Uart uart0 = new Uart(0);
        public static Uart uart1 = new Uart(1);


        public static uint maxLength = 10240;
        public static int baudRate = 9600;
        public static int parity = 0;
        public static int timeout = 50;
        public static bool bitDelay = true;
        public static int dataBits = 8;
        public static int stopBit = 1;



        public static bool hexSend = false;
        public static string sendScript = "default";
        public static bool extraEnter = false;
        public static int encoding = 65001;
        private static bool EnableSymbol = true;
        private static byte[] b_del = Encoding.GetEncoding(65001).GetBytes("␡");
        private static byte[][] symbols =
{
            new byte[]{226,144,128},new byte[]{226,144,129},new byte[]{226,144,130},new byte[]{226,144,131},new byte[]{226,144,132},
            new byte[]{226,144,133},new byte[]{226,144,134},new byte[]{226,144,135},new byte[]{226,144,136},new byte[]{226,144,137},
            new byte[]{226,144,138},new byte[]{226,144,139},new byte[]{226,144,140},new byte[]{226,144,141},new byte[]{226,144,142},
            new byte[]{226,144,143},new byte[]{226,144,144},new byte[]{226,144,145},new byte[]{226,144,146},new byte[]{226,144,147},
            new byte[]{226,144,148},new byte[]{226,144,149},new byte[]{226,144,150},new byte[]{226,144,151},new byte[]{226,144,152},
            new byte[]{226,144,153},new byte[]{226,144,154},new byte[]{226,144,155},new byte[]{226,144,156},new byte[]{226,144,157},
            new byte[]{226,144,158},new byte[]{226,144,159},
        };


        /// <summary>
        /// hex转byte
        /// </summary>
        /// <param name="mHex">hex值</param>
        /// <returns>原始字符串</returns>
        public static byte[] Hex2Byte(string mHex)
        {
            mHex = Regex.Replace(mHex, "[^0-9A-Fa-f]", "");
            if (mHex.Length % 2 != 0)
                mHex = mHex.Remove(mHex.Length - 1, 1);
            if (mHex.Length <= 0) return new byte[0];
            byte[] vBytes = new byte[mHex.Length / 2];
            for (int i = 0; i < mHex.Length; i += 2)
                if (!byte.TryParse(mHex.Substring(i, 2), NumberStyles.HexNumber, null, out vBytes[i / 2]))
                    vBytes[i / 2] = 0;
            return vBytes;
        }


        /// <summary>
        /// byte转string（可读）
        /// </summary>
        /// <param name="vBytes"></param>
        /// <returns></returns>
        public static string Byte2Readable(byte[] vBytes, int len = -1)
        {
            if (len == -1)
                len = vBytes.Length;
            if (vBytes == null)//fix
                return "";
            //没开这个功能/非utf8就别搞了
            if (!EnableSymbol || encoding != 65001)
                return Byte2String(vBytes, len);
            var tb = new List<byte>();
            for (int i = 0; i < len; i++)
            {
                switch (vBytes[i])
                {
                    case 0x0d:
                        //遇到成对出现
                        if (i < len - 1 && vBytes[i + 1] == 0x0a)
                        {
                            tb.AddRange(symbols[0x0d]);
                            tb.AddRange(symbols[0x0a]);
                            tb.Add(0x0d);
                            tb.Add(0x0a);
                            i++;
                        }
                        else
                        {
                            tb.AddRange(symbols[0x0d]);
                            tb.Add(vBytes[i]);
                        }
                        break;
                    case 0x0a:
                    case 0x09://tab字符
                        tb.AddRange(symbols[vBytes[i]]);
                        tb.Add(vBytes[i]);
                        break;
                    default:
                        //普通的字符
                        if (vBytes[i] <= 0x1f)
                            tb.AddRange(symbols[vBytes[i]]);
                        else if (vBytes[i] == 0x7f)//del
                            tb.AddRange(b_del);
                        else
                            tb.Add(vBytes[i]);
                        break;
                }
            }
            return GetEncoding().GetString(tb.ToArray());
        }

        public static string Byte2Hex(byte[] d, string s = "", int len = -1)
        {
            if (len == -1)
                len = d.Length;
            return BitConverter.ToString(d, 0, len).Replace("-", s);
        }

        /// <summary>
        /// byte转string
        /// </summary>
        /// <param name="mHex"></param>
        /// <returns></returns>
        public static string Byte2String(byte[] vBytes, int len = -1)
        {
            var br = from e in vBytes
                     where e != 0
                     select e;
            if (len == -1 || len > br.Count())
                len = br.Count();
            return GetEncoding().GetString(br.Take(len).ToArray());
        }

        public static Encoding GetEncoding() => Encoding.GetEncoding(encoding);
    }
}
