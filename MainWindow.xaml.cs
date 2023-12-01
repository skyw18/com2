using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.IO.Ports;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Text.RegularExpressions;
using System.Management;
using System.Diagnostics;
using ComTool.Properties;
using System.Windows.Markup;
using System.Threading;
using System.Collections;
using System.Windows.Threading;
using System.IO;
using static System.Net.Mime.MediaTypeNames;
using System.Xml.Linq;

namespace ComTool
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private Paragraph para1 = new Paragraph(); // Paragraph 类似于 html 的 P 标签 用于Uart0的 彩色日志输出
        private Paragraph para2 = new Paragraph(); // Paragraph 类似于 html 的 P 标签 用于Uart1的 彩色日志输出
        private Paragraph paraLog = new Paragraph(); // Paragraph 类似于 html 的 P 标签  用于lua脚本的日志输出
        public MainWindow()
        {
            InitializeComponent();

            this.Title = "ComTool " + Global.MajorVersion.ToString() + "." + Global.MinorVersion.ToString() + "." + Global.PatchVersion.ToString();
        }

        private bool refreshLock = false;
        /// <summary>
        /// 刷新设备列表
        /// </summary>
        private void refreshPortList()
        {
            if (refreshLock)
                return;
            refreshLock = true;
            cbPort1.Items.Clear();
            cbPort2.Items.Clear();
            List<string> strs = new List<string>();
            Task.Run(() =>
            {
                while (true)
                {
                    try
                    {
                        ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_PnPEntity");
                        Regex regExp = new Regex("\\(COM\\d+\\)");
                        foreach (ManagementObject queryObj in searcher.Get())
                        {
                            if ((queryObj["Caption"] != null) && regExp.IsMatch(queryObj["Caption"].ToString()))
                            {
                                strs.Add(queryObj["Caption"].ToString());
                            }
                        }
                        break;
                    }
                    catch
                    {
                        Task.Delay(500).Wait();
                    }
                    //MessageBox.Show("fail了");
                }

                try
                {
                    foreach (string p in SerialPort.GetPortNames())//加上缺少的com口
                    {
                        //有些人遇到了微软库的bug，所以需要手动从0x00截断
                        var pp = p;
                        if (p.IndexOf("\0") > 0)
                            pp = p.Substring(0, p.IndexOf("\0"));
                        bool notMatch = true;
                        foreach (string n in strs)
                        {
                            if (n.Contains($"({pp})"))//如果和选中项目匹配
                            {
                                notMatch = false;
                                break;
                            }
                        }
                        if (notMatch)
                            strs.Add($"Serial Port {pp} ({pp})");//如果列表中没有，就自己加上
                    }
                }
                catch { }


                this.Dispatcher.Invoke(new Action(delegate {
                    foreach (string i in strs)
                    {
                        cbPort1.Items.Add(i);
                        cbPort2.Items.Add(i);
                    }
                        
                    if (strs.Count >= 1)
                    {
                        bOpen1.IsEnabled = true;
                        bOpen2.IsEnabled = true;
                        cbPort1.SelectedIndex = 0;
                        cbPort2.SelectedIndex = 0;
                    }
                    else
                    {
                        bOpen1.IsEnabled = false;
                        bOpen2.IsEnabled = false;
                    }
                    refreshLock = false;
                }));
            });
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //延迟启动，加快软件第一屏出现速度
            Task.Run(() =>
            {
                this.Dispatcher.Invoke(new Action(delegate
                {
                    //接收到、发送数据成功回调
                    Global.uart0.UartDataRecived += Uart0_UartDataRecived;
                    Global.uart0.UartDataSent += Uart0_UartDataSent;

                    Global.uart1.UartDataRecived += Uart1_UartDataRecived;
                    Global.uart1.UartDataSent += Uart1_UartDataSent;
                    //加载初始波特率
                    cbRate1.Text = "9600";
                    cbRate1.SelectedIndex = 6;
                    cbRate2.Text = "9600";
                    cbRate2.SelectedIndex = 6;


                    Global.uart0.serial.BaudRate = Global.baudRate;
                    Global.uart0.serial.Parity = (Parity)Global.parity;
                    Global.uart0.serial.DataBits = Global.dataBits;
                    Global.uart0.serial.StopBits = (StopBits)Global.stopBit;

                    Global.uart1.serial.BaudRate = Global.baudRate;
                    Global.uart1.serial.Parity = (Parity)Global.parity;
                    Global.uart1.serial.DataBits = Global.dataBits;
                    Global.uart1.serial.StopBits = (StopBits)Global.stopBit;


                    //重写关闭窗口代码
                    this.Closing += MainWindow_Closing;

                    //加载lua日志打印事件
                    LuaEnv.LuaApis.PrintLuaLog += LuaApis_PrintLuaLog;
                    LuaEnv.LuaRunEnv.LuaRunError += LuaRunEnv_LuaRunError;

                    new Thread(LuaLogPrintTask).Start();




                    //热更，防止恶性bug，及时修复
                    new Thread(() =>
                    {
                        try
                        {
                            var lua = new LuaEnv.LuaEnv();
                        }
                        catch { }
                    }).Start();



                    refreshPortList();

                    FlowDocument Doc1 = new FlowDocument();
                    para1.LineHeight = 1;
                    para1.Margin = new Thickness(0, 0, 0, 0);
                    tBox1.Document = Doc1;
                    tBox1.Document.Blocks.Add(para1);


                    FlowDocument Doc2 = new FlowDocument();
                    para2.LineHeight = 1;
                    para2.Margin = new Thickness(0, 0, 0, 0);
                    tBox2.Document = Doc2;
                    tBox2.Document.Blocks.Add(para2);


                    FlowDocument DocLog = new FlowDocument();
                    paraLog.LineHeight = 1;
                    paraLog.Margin = new Thickness(0, 0, 0, 0);
                    tBoxLog.Document = DocLog;
                    tBoxLog.Document.Blocks.Add(paraLog);
                }));
            });
        }


        private void Uart0_UartDataSent(object sender, EventArgs e)
        {
            RefreshBox(0, true, Encoding.ASCII.GetString(sender as byte[]));
        }



        private void Uart0_UartDataRecived(object sender, EventArgs e)
        {
            RefreshBox(0, false, Encoding.ASCII.GetString(sender as byte[]));
        }


        private void Uart1_UartDataSent(object sender, EventArgs e)
        {
            RefreshBox(1, true, Encoding.ASCII.GetString(sender as byte[]));

        }

        private void Uart1_UartDataRecived(object sender, EventArgs e)
        {
            RefreshBox(1, false, Encoding.ASCII.GetString(sender as byte[]));
        }

        private void RefreshBox(int uartId, bool send, string log)
        {
            this.Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => {
                try
                {
                    var r_time = new Run(DateTime.Now.ToString("[HH:mm:ss:ffff] ")); // Run 是一个 Inline 的标签
                    r_time.Foreground = new SolidColorBrush(Global.TimeColor);//设置字体颜色

                    if (uartId == 0)
                    {
                        if (cbShow1Hex.IsChecked == true)
                        {
                            log = Global.Byte2Hex(Encoding.Default.GetBytes(log), " ", log.Length);
                        }
                        else
                        {
                            log = Global.Byte2Readable(Encoding.Default.GetBytes(log), log.Length);
                        }
                        var r_text = new Run(log + "\n");
                        Run r_com;

                        if (send)
                        {
                            r_com = new Run(Global.uart0.GetName() + "← ");
                            r_com.Foreground = new SolidColorBrush(Global.SendColorDark);
                            r_text.Foreground = new SolidColorBrush(Global.SendColor);
                        }
                        else
                        {
                            r_com = new Run(Global.uart0.GetName() + "→ ");
                            r_com.Foreground = new SolidColorBrush(Global.ReceiveColorDark);
                            r_text.Foreground = new SolidColorBrush(Global.ReceiveColor);
                        }
                       
                        para1.Inlines.Add(r_time);
                        para1.Inlines.Add(r_com);
                        para1.Inlines.Add(r_text);
                        tBox1.ScrollToEnd();
                    }
                    else if(uartId == 1)
                    {
                        if (cbShow2Hex.IsChecked == true)
                        {
                            log = Global.Byte2Hex(Encoding.Default.GetBytes(log), " ", log.Length);
                        }
                        else
                        {
                            log = Global.Byte2Readable(Encoding.Default.GetBytes(log), log.Length);
                        }
                        var r_text = new Run(log + "\n");
                        Run r_com;

                        if (send)
                        {
                            r_com = new Run(Global.uart1.GetName() + "← ");
                            r_com.Foreground = new SolidColorBrush(Global.SendColorDark);
                            r_text.Foreground = new SolidColorBrush(Global.SendColor);
                        }
                        else
                        {
                            r_com = new Run(Global.uart1.GetName() + "→ ");
                            r_com.Foreground = new SolidColorBrush(Global.ReceiveColorDark);
                            r_text.Foreground = new SolidColorBrush(Global.ReceiveColor);
                        }
                        para2.Inlines.Add(r_time);
                        para2.Inlines.Add(r_com);
                        para2.Inlines.Add(r_text);
                        tBox2.ScrollToEnd();
                    }
                }
                catch (Exception ex)
                {

                }

            }));
        }

        /// <summary>
        /// 窗口关闭事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Global.isMainWindowsClosed = true;
            //foreach (Window win in App.Current.Windows)
            //{
            //    if (win != this)
            //    {
            //        win.Close();
            //    }
            //}
            //e.Cancel = false;//正常关闭
        }


        private void LuaRunEnv_LuaRunError(object sender, EventArgs e)
        {
            luaLogPrintable = true;
        }

        //是否可打印标记
        private bool _luaLogPrintable = true;
        private bool luaLogPrintable
        {
            get
            {
                return _luaLogPrintable;
            }
            set
            {
                this.Dispatcher.Invoke(new Action(delegate
                {
                    if (value)
                    {

                    }
                    else
                    {

                    }
                }));
                _luaLogPrintable = value;
            }
        }

        private void bRun_Click(object sender, RoutedEventArgs e)
        {
            luaLogPrintable = true;
            LuaEnv.LuaRunEnv.New($"\\script\\main.lua");
            LuaEnv.LuaRunEnv.canRun = true;

        }

        //lua日志打印次数
        private int luaLogCount = 0;
        private EventWaitHandle luaWaitQueue = new AutoResetEvent(false);
        private List<string> luaLogsBuff = new List<string>();
        private void LuaApis_PrintLuaLog(object sender, EventArgs e)
        {
            DateTime.Now.ToString("[HH:mm:ss:ffff]");
            if (sender is string && sender != null)
            {
                lock (luaLogsBuff)
                {
                    if (luaLogsBuff.Count > 500)
                    {
                        luaLogsBuff.Clear();
                        luaLogsBuff.Add("too many logs!");
                        //延时0.5秒，防止卡住ui线程
                        Thread.Sleep(500);
                    }
                    else
                        luaLogsBuff.Add(sender as string);
                }
                luaWaitQueue.Set();
            }
        }

        private void AddLuaLog(string log)
        {
            var r_time = new Run(DateTime.Now.ToString("[HH:mm:ss:ffff] "));
            r_time.Foreground = new SolidColorBrush(Global.TimeColor);
            paraLog.Inlines.Add(r_time);
            
            var r_text = new Run(log);
            r_text.Foreground = new SolidColorBrush(Global.LogColor);
            paraLog.Inlines.Add(r_text);

           tBoxLog.ScrollToEnd();
        }


        private void LuaLogPrintTask()
        {
            luaWaitQueue.Reset();
            Global.ProgramClosedEvent += (object a, EventArgs b) =>
            {
                luaWaitQueue.Set();
            };
            while (true)
            {
                luaWaitQueue.WaitOne();
                if (Global.isMainWindowsClosed)
                    return;
                var logsb = new StringBuilder();
                lock (luaLogsBuff)
                {
                    for (int i = 0; i < luaLogsBuff.Count; i++)
                    {
                        logsb.AppendLine(luaLogsBuff[i]);
                        luaLogCount++;
                    }
                    luaLogsBuff.Clear();
                }

                if (!luaLogPrintable)
                    continue;
                if (logsb.Length == 0)
                    continue;
                var logs = logsb.ToString();
                DoInvoke(() =>
                {
                    tBoxLog.IsEnabled = false;//确保文字不再被选中，防止wpf卡死
                    if (luaLogCount >= 1000)
                    {
                        tBoxLog.Document.Blocks.Clear();
                        paraLog = new Paragraph();
                        tBoxLog.Document.Blocks.Add(paraLog);
                        AddLuaLog("Lua log too long, auto clear.\r\n" +
                            "more logs see lua log file.\r\n");
                        luaLogCount = 0;
                    }
                    AddLuaLog(logs);
                    tBoxLog.ScrollToEnd();
                    if (!tBoxLog.IsMouseOver)
                        tBoxLog.IsEnabled = true;
                });
                //正常就延时10ms，防止卡住ui线程
                Thread.Sleep(10);
            }
        }

        private bool DoInvoke(Action action)
        {
            if (Global.isMainWindowsClosed)
                return false;
            Dispatcher.Invoke(action);
            return true;
        }

        private void bOpen1_Click(object sender, RoutedEventArgs e)
        {
            if(Global.uart0.IsOpen()) //如果已打开，则关闭串口
            {
                try
                {
                    Global.uart0.Close();
                    bOpen1.Content = "打开串口0";
                    bSend1.IsEnabled = false;
                    cbPort1.IsEnabled = true;
                    cbRate1.IsEnabled = true;
                }
                catch
                {
                    MessageBox.Show("串口关闭失败");
                    return;
                }
            }
            else
            {
                try
                {
                    string comName = "";
                    int rate;
                    int bit = 8;

                    if (cbPort1.SelectedIndex == -1)
                    {
                        MessageBox.Show("请选择正确串口");
                        return;
                    }

                    //comName = cbPort1.Items[cbPort1.SelectedIndex].ToString();
                    if (cbPort1.SelectedItem != null)
                    {
                        string[] ports;//获取所有串口列表
                        try
                        {
                            ports = SerialPort.GetPortNames();
                        }
                        catch (Exception)
                        {
                            ports = new string[0];
                        }
                        foreach (string p in ports)//循环查找符合名称串口
                        {
                            //有些人遇到了微软库的bug，所以需要手动从0x00截断
                            var pp = p;
                            if (p.IndexOf("\0") > 0)
                                pp = p.Substring(0, p.IndexOf("\0"));
                            if ((cbPort1.SelectedItem as string).Contains($"({pp})"))//如果和选中项目匹配
                            {
                                comName = pp;
                                break;
                            }
                        }
                    }


                    if (int.TryParse(cbRate1.Text.ToString(), out rate) == false)
                    {
                        MessageBox.Show("请输入正确波特率数字");
                        return;
                    }



                    Global.uart0.SetName(comName);
                    Global.uart0.Open();

                    bOpen1.Content = "关闭串口0";
                    bSend1.IsEnabled = true;
                    cbPort1.IsEnabled = false;
                    cbRate1.IsEnabled = false;
                }
                catch
                {
                    MessageBox.Show("连接失败");
                    return;
                }
            }
        }


        private void bOpen2_Click(object sender, RoutedEventArgs e)
        {
            if (Global.uart1.IsOpen()) //如果已打开，则关闭串口
            {
                try
                {
                    Global.uart1.Close();
                    bOpen2.Content = "打开串口1";
                    bSend2.IsEnabled = false;
                    cbPort2.IsEnabled = true;
                    cbRate2.IsEnabled = true;
                }
                catch
                {
                    MessageBox.Show("串口关闭失败");
                    return;
                }
            }
            else
            {
                try
                {
                    string comName = "";
                    int rate;
                    int bit = 8;

                    if (cbPort2.SelectedIndex == -1)
                    {
                        MessageBox.Show("请选择正确串口");
                        return;
                    }

                    //comName = cbPort1.Items[cbPort1.SelectedIndex].ToString();
                    if (cbPort2.SelectedItem != null)
                    {
                        string[] ports;//获取所有串口列表
                        try
                        {
                            ports = SerialPort.GetPortNames();
                        }
                        catch (Exception)
                        {
                            ports = new string[0];
                        }
                        foreach (string p in ports)//循环查找符合名称串口
                        {
                            //有些人遇到了微软库的bug，所以需要手动从0x00截断
                            var pp = p;
                            if (p.IndexOf("\0") > 0)
                                pp = p.Substring(0, p.IndexOf("\0"));
                            if ((cbPort2.SelectedItem as string).Contains($"({pp})"))//如果和选中项目匹配
                            {
                                comName = pp;
                                break;
                            }
                        }
                    }


                    if (int.TryParse(cbRate2.Text.ToString(), out rate) == false)
                    {
                        MessageBox.Show("请输入正确波特率数字");
                        return;
                    }

                    Global.uart1.SetName(comName);
                    Global.uart1.Open();

                    bOpen2.Content = "关闭串口1";
                    bSend2.IsEnabled = true;
                    cbPort2.IsEnabled = false;
                    cbRate2.IsEnabled = false;
                }
                catch
                {
                    MessageBox.Show("连接失败");
                    return;
                }
            }
        }


        private void bSend1_Click(object sender, RoutedEventArgs e)
        {
            byte[] dataConvert;
            if(cbSend1Hex.IsChecked == true)
            {
                dataConvert = Global.Hex2Byte(tBoxSend.Text);
            }
            else
            {
                dataConvert = Encoding.GetEncoding(65001).GetBytes(tBoxSend.Text);
            }

            Global.uart0.SendData(dataConvert);
        }

        private void bSend2_Click(object sender, RoutedEventArgs e)
        {
            byte[] dataConvert;
            if (cbSend2Hex.IsChecked == true)
            {
                dataConvert = Global.Hex2Byte(tBoxSend.Text);
            }
            else
            {
                dataConvert = Encoding.GetEncoding(65001).GetBytes(tBoxSend.Text);
            }

            Global.uart1.SendData(dataConvert);
        }


        private void bOpenDir_Click(object sender, RoutedEventArgs e)
        {
            if(!Directory.Exists(Environment.CurrentDirectory + "\\script"))
            {
                Directory.CreateDirectory(Environment.CurrentDirectory + "\\script");
            }
            Process.Start("Explorer.exe", Environment.CurrentDirectory + "\\script");
        }

        private void bClearScriptLog_Click(object sender, RoutedEventArgs e)
        {
            tBoxLog.Document.Blocks.Clear();
            paraLog = new Paragraph();
            tBoxLog.Document.Blocks.Add(paraLog);
        }

        private void bClear1_Click(object sender, RoutedEventArgs e)
        {
            tBox1.Document.Blocks.Clear();
            para1 = new Paragraph();
            tBox1.Document.Blocks.Add(para1);
        }

        private void bClear2_Click(object sender, RoutedEventArgs e)
        {
            tBox2.Document.Blocks.Clear();
            para2 = new Paragraph();
            tBox2.Document.Blocks.Add(para2);
        }

        private void cbRate2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbRate2.SelectedIndex == -1)
            {
                return;
            }
            int tmpBaudRate = Global.baudRate;
            int.TryParse((cbRate2.SelectedItem as ComboBoxItem).Content.ToString(), out tmpBaudRate);
            Global.uart1.serial.BaudRate = tmpBaudRate;
        }

        private void cbRate1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(cbRate1.SelectedIndex == -1)
            {
                return;
            }

            int tmpBaudRate = Global.baudRate;
            int.TryParse((cbRate1.SelectedItem as ComboBoxItem).Content.ToString(),out tmpBaudRate);
            Global.uart0.serial.BaudRate = tmpBaudRate;
        }
    }
}
