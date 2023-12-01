using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComTool.LuaEnv
{
    class LuaApis
    {
        public static event EventHandler PrintLuaLog;
        /// <summary>
        /// 打印日志
        /// </summary>
        /// <param name="log">日志内容</param>
        public static void PrintLog(string log)
        {
            PrintLuaLog(log, EventArgs.Empty);
        }

        /// <summary>
        /// utf8编码改为gbk的hex编码
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string Utf8ToAsciiHex(string input)
        {
            return BitConverter.ToString(Encoding.GetEncoding("GB2312").GetBytes(input)).Replace("-","");
        }


        /// <summary>
        /// utf8编码改为gbk的hex编码
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static byte[] Ascii2Utf8(byte[] input)
        {
            return Encoding.UTF8.GetBytes(Encoding.Default.GetString(input));
        }

        /// <summary>
        /// 获取程序运行目录
        /// </summary>
        /// <returns>主程序运行目录（win10商店时返回appdata路径）</returns>
        public static string GetPath()
        {
            return Global.ProfilePath;
        }


        /// <summary>
        /// 发送通道的回调函数
        /// </summary>
        private static Dictionary<string, Func<byte[], XLua.LuaTable, bool>> SendChannels = new Dictionary<string, Func<byte[], XLua.LuaTable, bool>>();
        public static void SendChannelsRegister(string channel, Func<byte[], XLua.LuaTable, bool> cb) => SendChannels[channel] = cb;

        /// <summary>
        /// 发送数据到通用通道
        /// </summary>
        /// <param name="channel">通道名称</param>
        /// <param name="data">数据</param>
        /// <returns>发送是否成功</returns>
        public static bool Send(string channel, byte[] data, XLua.LuaTable table)
        {
            if (SendChannels.ContainsKey(channel))
                return SendChannels[channel](data, table);
            return false;
        }

        /// <summary>
        /// 通用通道收到消息
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="data"></param>
        public static void SendChannelsReceived(string channel, object data) => LuaRunEnv.ChannelReceived(channel, data);
    }
}
