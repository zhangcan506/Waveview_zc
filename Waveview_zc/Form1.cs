using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using System.Media;
using WMPLib;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Collections;
using System.Diagnostics;//引用相关的命名空间
using System.Runtime.InteropServices;

namespace Waveview_zc
{
    public partial class Form_zc : Form
    {
        public Form_zc()
        {
            InitializeComponent();
            CheckForIllegalCrossThreadCalls = false;//可以使得新创建的线程访问UI线程创建的窗口控件
                                                    // 也可以针对某一控件进行设置   TextBox.CheckForIllegalCrossThreadCalls = false;
                                                    //用户代码，初始化全局用显示框大小，格子间隔大小
            if (this.pictureBox.InvokeRequired)
            {//使用委托来赋值
                this.pictureBox.Invoke(new EventHandler(delegate
                {
                    WorkSpaceWidth = this.pictureBox.Width;//全局宽度
                    WorkSpaceHeight = this.pictureBox.Height;//全局高度
                }));//参数
            }
            else
            {
                WorkSpaceWidth = this.pictureBox.Width;//全局宽度
                WorkSpaceHeight = this.pictureBox.Height;//全局高度
            }

            WorkSpaceVolTotal = (int)(WorkSpaceHeight - WorkSpaceVolStart) / WorkSpaceVolInterval;//一共22行格子
            WorkSpaceScaleTotal = (int)(WorkSpaceWidth - WorkSpaceScaleStart) / WorkSpaceScaleInterval;//一共19 列格子
            WavScale= (float)WorkSpaceWidth/ WorkSpaceScaleInterval;//浮点型 多少个列格子34.79
            // 测试暴露的问题   每台计算机  像素不一样，导致，计算出来的格子数不一样
            init();
            wavBmp = new Bitmap(WorkSpaceWidth, WorkSpaceHeight);
        }

        //定义用户成员变量
        Bitmap wavBmp;
        byte[] wavData;
        WaveFile wf;//  wav文件  类实例  将
        byte[] waveFile = null;//wav文件  字节流文件

        WindowsMediaPlayer wmp = new WindowsMediaPlayer();//定义媒体播放类
        Point[] Datapoint;
        bool playstatus = false;

        float WavScale;//工作区域长度/固定间隔
        const int beishu = 6;
        double totalTime = 0.001;
        uint num = 0;
        uint num_samples = 0;
        ushort inter_time = 10;//表明 我需要10ms 格式化数据
       // ushort[] array_intersample;
        float samples_per_intertime = 0;
        ////////////////////////////关于背景表格刻度尺
        const int GridWidth = 5;
        int WorkSpaceWidth = 1000;
        int WorkSpaceHeight = 450;

        const int WorkSpaceVolStart = 10;//   行 开始
        const int WorkSpaceVolInterval = 20;//   行 间隔
        int WorkSpaceVolTotal = 1;//

        const int WorkSpaceScaleStart = 0;//   列开始  方便计算刻度
        const int WorkSpaceScaleInterval = 50;//   列间隔
        int WorkSpaceScaleTotal = 1;//

        class MyDatastruct
        {
            public float time;
            public byte[] Led_data;  //17字节   LED报文
            public string explain = "";
            public MyDatastruct(float t, byte[] Led)
            {
                time = t;
                Led_data = Led;
            }
        };
        List<MyDatastruct> Data_array = new List<MyDatastruct>();  // 标定点数组  声明
        int Data_array_index=0, last_Data_array_index = 0;//播放时发送LED报文有用到
        Stopwatch st = new Stopwatch();//实例化类
        ////////////////////////////
       // long miliseconds1 = 0, miliseconds2 = 0;
        double temp;
        ///////////////////////////定义网络UDP发送设置
        int _port = 8800;
        int _remotePort = 8899;
        string _remoteIP;
        IPEndPoint _remoteEndPoint;
        UdpClient _udpClient;
        ///////////////////////////定义LED数据区
        byte[] LEDdata = new byte[17];
        byte[] LEDdata_black = new byte[17];
        byte R1 = 0, G1 = 0, B1 = 0;
        byte R2 = 0, G2 = 0, B2 = 0;
        byte R3 = 0, G3 = 0, B3 = 0;
        byte LEDmode=1,speed = 0, density = 0, light = 0, led_number = 1;
        string jieshi = "";
        //结束定义成员变量

        //初始化连接
        void init()
        {
            if (_udpClient != null)
            {
                _udpClient.Close();
            }
            _remoteIP = this.txtRemoteHost.Text;
            _remotePort = int.Parse(this.txtRemotePort.Text);
            _port = int.Parse(this.txtport.Text);
            _remoteEndPoint = new IPEndPoint(IPAddress.Parse(_remoteIP), _remotePort);
            _udpClient = new UdpClient(_port);//本地端口

        }
        /// <summary>
        /// 用户函数，画背景显示图
        /// </summary>
        public void drawBackGround() //background draw  画背景 格子
        {
            //background draw  画背景 格子
            Graphics gBG = Graphics.FromImage(wavBmp);
            Rectangle rect = new Rectangle(0, 0, WorkSpaceWidth, WorkSpaceHeight);//画矩形  外框  左上角为（0，0）原点

            //black background  黑色背景
            SolidBrush sb = new SolidBrush(Color.Black);//黑色画笔
            gBG.FillRectangle(sb, rect);//在画好的黑色矩形里填充 黑色
            sb.Dispose();//释放sb画笔

            //line   画线部分
            Pen myPen = new Pen(Color.Blue, 1);
            myPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;//定义画线的样式：虚线样式
            myPen.DashPattern = new float[] { 4, 2 };//自定义短划线样式  4实 2虚

            //H   画水平线   画格子行
            for (int i = 0; i < WorkSpaceVolTotal; i++)//格子行数
            {
                int y = WorkSpaceVolStart + i * WorkSpaceVolInterval;
                gBG.DrawLine(myPen, 0, y, pictureBox.Size.Width, y);
            }

            //V   画垂直线  画格子列
            for (int i = 0; i < WorkSpaceScaleTotal+1; i++)//格子列数
            {
                int x = WorkSpaceScaleStart + i * WorkSpaceScaleInterval;
                gBG.DrawLine(myPen, x, 0, x, pictureBox.Size.Height);
               // drawTimeScale((i + 1) / 2, x);
            }

            myPen.Dispose();//释放画笔

            //center line  画中心线
            Pen myRedPen = new Pen(Color.Red, 1);
            myRedPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Solid;
            gBG.DrawLine(myRedPen, 0, WorkSpaceHeight / 2, WorkSpaceWidth, WorkSpaceHeight / 2);
            myRedPen.Dispose();
            //bottom line  画底部线2  用来显示标定点的
            Pen myGreenPen = new Pen(Color.Gold, 1);
            myGreenPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Solid;
            gBG.DrawLine(myGreenPen, 0, WorkSpaceHeight - 50, WorkSpaceWidth, WorkSpaceHeight - 50);
            myGreenPen.Dispose();
            gBG.Dispose();//释放画笔
            //wavBmp.Dispose();
        }
        float BackGround_first_time = 0;
        float BackGround_end_time = 0;
        float BackGround_interval_time = 0;
        float already_passed_time = 0;
        public void TimeScaleValue()//水平时间内容重写
        {
            float temp00 = 0;
            for (int i = 2; i < WorkSpaceScaleTotal + 1; )
            {
                temp00 = BackGround_first_time + (BackGround_interval_time * i);
                drawTimeScale_labNum((i-2)/2, temp00);
                i=i+2;
            }
            
        }
        public void DrawTimeScale()//水平刻度对齐
        {
            int x = 0;
            for (int i = 2; i < WorkSpaceScaleTotal + 1; )
            {
                x = WorkSpaceScaleStart + i * WorkSpaceScaleInterval;
                drawTimeScale_align((i-2) / 2, x);
                i=i+2;
            }

        }
        public void drawTimeScale_labNum(int index, double number)//更新某个时间刻度标签
        {//更新某个时间刻度标签
            switch (index)
            {
                case 0:
                    if (this.labNum1.InvokeRequired)
                    {//使用委托来赋值
                        this.labNum1.Invoke(new EventHandler(delegate
                        {
                            labNum1.Text = string.Format("{0,-3}", number.ToString("f2"));
                        }));//参数
                    }
                    else
                    {
                        labNum1.Text = string.Format("{0,-3}", number.ToString("f2"));
                    }
                    break;
                case 1:
                    if (this.labNum2.InvokeRequired)
                    {//使用委托来赋值
                        this.labNum2.Invoke(new EventHandler(delegate
                        {
                            labNum2.Text = string.Format("{0,-3}", number.ToString("f2"));
                        }));//参数
                    }
                    else
                    {
                        labNum2.Text = string.Format("{0,-3}", number.ToString("f2"));
                    }
                    break;
                case 2: 
                    if (this.labNum3.InvokeRequired)
                    {//使用委托来赋值
                        this.labNum3.Invoke(new EventHandler(delegate
                        {
                            labNum3.Text = string.Format("{0,-3}", number.ToString("f2"));
                        }));//参数
                    }
                    else
                    {
                        labNum3.Text = string.Format("{0,-3}", number.ToString("f2"));
                    }
                    break;
                case 3:    
                    if (this.labNum4.InvokeRequired)
                    {//使用委托来赋值
                        this.labNum4.Invoke(new EventHandler(delegate
                        {
                            labNum4.Text = string.Format("{0,-3}", number.ToString("f2"));
                        }));//参数
                    }
                    else
                    {
                        labNum4.Text = string.Format("{0,-3}", number.ToString("f2"));
                    }
                    break;
                case 4:                 
                    if (this.labNum5.InvokeRequired)
                    {//使用委托来赋值
                        this.labNum5.Invoke(new EventHandler(delegate
                        {
                            labNum5.Text = string.Format("{0,-3}", number.ToString("f2"));
                        }));//参数
                    }
                    else
                    {
                        labNum5.Text = string.Format("{0,-3}", number.ToString("f2"));
                    }
                    break;
                case 5:   
                    if (this.labNum6.InvokeRequired)
                    {//使用委托来赋值
                        this.labNum6.Invoke(new EventHandler(delegate
                        {
                            labNum6.Text = string.Format("{0,-3}", number.ToString("f2"));
                        }));//参数
                    }
                    else
                    {
                        labNum6.Text = string.Format("{0,-3}", number.ToString("f2"));
                    }
                    break;
                case 6:          
                    if (this.labNum7.InvokeRequired)
                    {//使用委托来赋值
                        this.labNum7.Invoke(new EventHandler(delegate
                        {
                            labNum7.Text = string.Format("{0,-3}", number.ToString("f2"));
                        }));//参数
                    }
                    else
                    {
                        labNum7.Text = string.Format("{0,-3}", number.ToString("f2"));
                    }
                    break;
                case 7:       
                    if (this.labNum8.InvokeRequired)
                    {//使用委托来赋值
                        this.labNum8.Invoke(new EventHandler(delegate
                        {
                            labNum8.Text = string.Format("{0,-3}", number.ToString("f2"));
                        }));//参数
                    }
                    else
                    {
                        labNum8.Text = string.Format("{0,-3}", number.ToString("f2"));
                    }
                    break;
                case 8:
                    if (this.labNum9.InvokeRequired)
                    {//使用委托来赋值
                        this.labNum9.Invoke(new EventHandler(delegate
                        {
                            labNum9.Text = string.Format("{0,-3}", number.ToString("f2"));
                        }));//参数
                    }
                    else
                    {
                        labNum9.Text = string.Format("{0,-3}", number.ToString("f2"));
                    }
                    break;
                case 9:   
                    if (this.labNum10.InvokeRequired)
                    {//使用委托来赋值
                        this.labNum10.Invoke(new EventHandler(delegate
                        {
                            labNum10.Text = string.Format("{0,-3}", number.ToString("f2"));
                        }));//参数
                    }
                    else
                    {
                        labNum10.Text = string.Format("{0,-3}", number.ToString("f2"));
                    }
                    break;
                case 10:
                    if (this.labNum11.InvokeRequired)
                    {//使用委托来赋值
                        this.labNum11.Invoke(new EventHandler(delegate
                        {
                            labNum11.Text = string.Format("{0,-3}", number.ToString("f2"));
                        }));//参数
                    }
                    else
                    {
                        labNum11.Text = string.Format("{0,-3}", number.ToString("f2"));
                    }
                    break;
                case 11: 
                    if (this.labNum12.InvokeRequired)
                    {//使用委托来赋值
                        this.labNum12.Invoke(new EventHandler(delegate
                        {
                            labNum12.Text = string.Format("{0,-3}", number.ToString("f2"));
                        }));//参数
                    }
                    else
                    {
                        labNum12.Text = string.Format("{0,-3}", number.ToString("f2"));
                    }
                    break;
                case 12: 
                    if (this.labNum13.InvokeRequired)
                    {//使用委托来赋值
                        this.labNum13.Invoke(new EventHandler(delegate
                        {
                            labNum13.Text = string.Format("{0,-3}", number.ToString("f2"));
                        }));//参数
                    }
                    else
                    {
                        labNum13.Text = string.Format("{0,-3}", number.ToString("f2"));
                    }
                    break;
                case 13:
                    if (this.labNum14.InvokeRequired)
                    {//使用委托来赋值
                        this.labNum14.Invoke(new EventHandler(delegate
                        {
                            labNum14.Text = string.Format("{0,-3}", number.ToString("f2"));
                        }));//参数
                    }
                    else
                    {
                        labNum14.Text = string.Format("{0,-3}", number.ToString("f2"));
                    }
                    break;
                case 14:
                    if (this.labNum15.InvokeRequired)
                    {//使用委托来赋值
                        this.labNum15.Invoke(new EventHandler(delegate
                        {
                            labNum15.Text = string.Format("{0,-3}", number.ToString("f2"));
                        }));//参数
                    }
                    else
                    {
                        labNum15.Text = string.Format("{0,-3}", number.ToString("f2"));
                    }
                    break;
                case 15:
                    if (this.labNum16.InvokeRequired)
                    {//使用委托来赋值
                        this.labNum16.Invoke(new EventHandler(delegate
                        {
                            labNum16.Text = string.Format("{0,-3}", number.ToString("f2"));
                        }));//参数
                    }
                    else
                    {
                        labNum16.Text = string.Format("{0,-3}", number.ToString("f2"));
                    }
                    break;
                case 16:
                    if (this.labNum17.InvokeRequired)
                    {//使用委托来赋值
                        this.labNum17.Invoke(new EventHandler(delegate
                        { labNum17.Text = string.Format("{0,-3}", number.ToString("f2"));
                        }));//参数
                    } else{
                        labNum17.Text = string.Format("{0,-3}", number.ToString("f2"));
                    }
                    break;
                default:
                    break;
            }
        }
        public void drawTimeScale_align(int index, int number)//更新某个时间刻度标签
        {//更新某个时间刻度标签
            switch (index)
            {
                case 0:
                    labNum1.Left = number;
                    labNum1.Height = WorkSpaceHeight+5;
                   // labNum1.Show();
                    break;
                case 1:
                    labNum2.Left = number;
                    labNum2.Height = WorkSpaceHeight + 5;
                 //   labNum2.Show();
                    break;
                case 2:
                    labNum3.Left = number;
                    labNum3.Height = WorkSpaceHeight + 5;
                   // labNum3.Show();
                    break;
                case 3:
                    labNum4.Left = number;
                    labNum4.Height = WorkSpaceHeight + 5;
                   // labNum4.Show();
                    break;
                case 4:
                    labNum5.Left = number;
                    labNum5.Height = WorkSpaceHeight + 5;
                    //labNum5.Show();
                    break;
                case 5:
                    labNum6.Left = number;
                    labNum6.Height = WorkSpaceHeight + 5;
                   // labNum6.Show();
                    break;
                case 6:
                    labNum7.Left = number;
                    labNum7.Height = WorkSpaceHeight + 5;
                   // labNum7.Show();
                    break;
                case 7:
                    labNum8.Left = number;
                    labNum8.Height = WorkSpaceHeight + 5;
                   // labNum8.Show();
                    break;
                case 8:
                    labNum9.Left = number;
                    labNum9.Height = WorkSpaceHeight + 5;
                   // labNum9.Show();
                    break;
                case 9:
                    labNum10.Left = number;
                    labNum10.Height = WorkSpaceHeight + 5;
                   // labNum10.Show();
                    break;
                case 10:
                    labNum11.Left = number;
                    labNum11.Height = WorkSpaceHeight + 5;
                   // labNum11.Show();
                    break;
                case 11:
                    labNum12.Left = number;
                    labNum12.Height = WorkSpaceHeight + 5;
                   // labNum12.Show();
                    break;
                case 12:
                    labNum13.Left = number;
                    labNum13.Height = WorkSpaceHeight + 5;
                   // labNum13.Show();
                    break;
                case 13:
                    labNum14.Left = number;
                    labNum14.Height = WorkSpaceHeight + 5;
                  //  labNum14.Show();
                    break;
                case 14:
                    labNum15.Left = number;
                    labNum15.Height = WorkSpaceHeight + 5;
                   // labNum15.Show();
                    break;
                case 15:
                    labNum16.Left = number;
                    labNum16.Height = WorkSpaceHeight + 5;
                  //  labNum16.Show();
                    break;
                case 16:
                    labNum17.Left = number;
                    labNum17.Height = WorkSpaceHeight + 5;
                  //  labNum17.Show();
                    break;
                default:
                    break;
            }
        }
        uint num_start = 0, num_end = 0;//定义开始样本，结束样本编号
                                        /// <summary>
                                        /// 用户函数，画波形图
                                        /// </summary>
        int vScrollBar_Value = 0, vScrollBar_Maximum = 0;
        int hScrollBar_Value = 0, hScrollBar_Maximum = 0;
        public void drawWav_zc()//画波形图 张灿重写
        {//画波形图          
            Point[] point; //水平显示点的个数   1000   从Datapoint 数组里 映射
            drawBackGround();//画背景            
            TimeScaleValue();//水平时间刻度标签时间内容更新
            if (wavData == null)//数据空时，保护
            {
                if (this.pictureBox.InvokeRequired)
                {//使用委托来赋值
                    this.pictureBox.Invoke(new EventHandler(delegate
                    {
                        pictureBox.Image = Image.FromHbitmap(wavBmp.GetHbitmap());
                    }));//参数
                    pictureBox.Refresh();
                }
                else
                {
                    pictureBox.Image = Image.FromHbitmap(wavBmp.GetHbitmap());
                    pictureBox.Refresh();
                }
                return;
            }

            //设置好画笔待用
            Graphics gBG = Graphics.FromImage(wavBmp);
            Pen myRedPen = new Pen(Color.Red, 1);//定义画笔
            myRedPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Solid;//定义画笔样式
            uint paint_length = 0;

            Pen myGoldPen = new Pen(Color.Gold, 1);
            myGoldPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Solid;
            //知道了BackGround_first_time  BackGround_interval_time  WavScale 查找Data_array数组中，时间在 开始时间 末尾时间之间的所有点，并画出来
            int start_index = 0, end_index = 0,xx=0;

            if (vScrollBar_Value == (vScrollBar_Maximum ))//图片范围最大  全频谱图时
            {
               // BackGround_first_time = 0;//图片范围最大时，首标定时间为0
               // BackGround_interval_time = (float)totalTime/WavScale;
               // BackGround_end_time = (float)totalTime;
                float scal = (float)num / (float)WorkSpaceWidth;
                //  point = new Point[num];
                //方法一：如下  每个采样点都画
                //方法二：如下  间隔画采样点 一共WorkSpaceWidth 个采样点    效果太粗糙
                //方法三：如下  间隔画采样点 一共100 * WorkSpaceWidth 个采样点  效果比较均衡
                point = Datapoint;
                if (scal > 100)//采样点是  像素点WorkSpaceWidth  的100倍以上
                {
                    paint_length = (uint)WorkSpaceWidth * 100;
                    scal = scal / 100;
                    for (uint i = 0; i < (paint_length - 2); i++)
                    {
                        gBG.DrawLine(myRedPen, i / 100, point[(uint)(scal * i)].Y, (i + 1) / 100, point[(uint)(scal * (i + 1))].Y);
                    }
                    point = null;
                }
                else
                {//采用  每个点都画的方法
                    paint_length = (uint)num;
                    for (uint i = 0; i < (paint_length - 2); i++)
                    {
                        gBG.DrawLine(myRedPen, (int)((float)i / scal), point[i].Y, (int)((float)(i + 1) / scal), point[i + 1].Y);
                    }
                    point = null;
                }
                if (Data_array.Count > 0)
                {
                    start_index = 0; end_index = Data_array.Count - 1;
                    //画标定点                   
                    for (int i = 0; i < Data_array.Count; i++)
                    {
                        xx = (int)(((float)Data_array[i].time / totalTime) * WorkSpaceWidth);
                        gBG.DrawLine(myGoldPen, xx, WorkSpaceHeight - 80, xx, WorkSpaceHeight - 20);
                    }
                }
                else
                {
                    start_index = 0; end_index = 0;
                }
            }
            else//图片中间范围
            {
                point = new Point[num_end - num_start+1];
                Array.Copy(Datapoint, num_start, point, 0,num_end - num_start);

                //BackGround_first_time = ((float)hScrollBar.Value*(float)totalTime)/(current_beishu_num);
               // BackGround_interval_time = (float)totalTime / (WavScale* current_beishu_num);
              //  BackGround_end_time = BackGround_first_time + BackGround_interval_time * WavScale;

                float scal = (float)(num_end - num_start+1) / (float)WorkSpaceWidth;

                if (scal > 100)//采样点是  像素点WorkSpaceWidth  的100倍以上
                {
                    paint_length = (uint)WorkSpaceWidth * 100;
                    scal = scal / 100;
                    for (uint i = 0; i < (paint_length - 2); i++)
                    {
                        gBG.DrawLine(myRedPen, i / 100, point[(uint)(scal * i)].Y, (i + 1) / 100, point[(uint)(scal * (i + 1))].Y);
                    }
                    point = null;
                }
                else
                {//采用  每个点都画的方法
                    paint_length = (uint)(num_end - num_start);
                    for (uint i = 0; i < (paint_length - 1); i++)
                    {
                        gBG.DrawLine(myRedPen, (int)((float)i / scal), point[i].Y, (int)((float)(i + 1) / scal), point[i + 1].Y);
                    }
                    point = null;
                }
                //得到需要显示的标定点
               
                if(Data_array.Count > 0)
                {
                    start_index = 0; end_index = Data_array.Count - 1;
                    for (int i = 0; i < Data_array.Count; i++)
                    {//要求每时每刻Data_array数组都是有序的
                       if (Data_array[i].time < BackGround_first_time)
                            start_index = i + 1;
                        if (Data_array[i].time > BackGround_end_time)
                        {
                            end_index = i - 1;//一旦找到立即退出，此时start_index一定先找到
                            break;
                        }
                     }
                    //画标定点               
                    for (int i = start_index; i <=end_index; i++)
                    {
                        xx = (int)(((float)(Data_array[i].time- BackGround_first_time)/ (BackGround_interval_time * WavScale)) * WorkSpaceWidth);
                        gBG.DrawLine(myGoldPen, xx, WorkSpaceHeight - 80, xx, WorkSpaceHeight - 20);
                    }
                }
               else
                {
                    start_index = 0; end_index = 0;
                }

            }
            point = null;
            myRedPen.Dispose();
            myGoldPen.Dispose();
            gBG.Dispose();// 局部变量  Graphics gBG 画图类
            if (this.pictureBox.InvokeRequired)
            {//使用委托来赋值
                //pictureBox.Image.Dispose();
                this.pictureBox.Invoke(new EventHandler(delegate
                {
                    pictureBox.Image = Image.FromHbitmap(wavBmp.GetHbitmap());
                }));//参数
                pictureBox.Refresh();
                
            }
            else
            {
                //pictureBox.Image.Dispose();//将原先pictureBox.Image占资源 释放掉
                pictureBox.Image = Image.FromHbitmap(wavBmp.GetHbitmap()); //创建的 GDI 位图对象的句柄
                pictureBox.Refresh();
            }
            //TimeScaleDisp();//水平时间刻度显示
            //以下 测试用            
         /*   if (this.textBox_receive.InvokeRequired)
            {//使用委托来取值 
                this.textBox_receive.Invoke(new EventHandler(delegate
                {
                    textBox_receive.Text = Convert.ToString(start_index)+"  "+ Convert.ToString(end_index);
                }));//参数
            }
            else
            {
                textBox_receive.Text = Convert.ToString(start_index) + "  " + Convert.ToString(end_index);
                // textBox_receive.Text = string.Format("{0,-3}", BackGround_first_time.ToString("f3")) + "  " + string.Format("{0,-3}", BackGround_end_time.ToString("f3"));
            }
            */
        }
        /// <summary>
        /// 用户函数，解码程序，将wav格式主要数据部分解码出来，输入编码值，返回解码值
        /// </summary>
        /// <param name="temp"></param>
        /// <returns></returns>
        public int wavYCal(uint temp)
        {
            int Y = 0;

            if (temp <= 0x1000)
            {
                if (temp == 0x0000)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 8;
                }
                else if (temp == 0x1000)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 7;
                }
                else if ((temp & 0x0FFF) < 0x0400)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 8 - 3;
                }
                else if ((temp & 0x0FFF) < 0x0700)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 8 - 6;
                }
                else if ((temp & 0x0FFF) < 0x0A00)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 8 - 10;
                }
                else if ((temp & 0x0FFF) < 0x0D00)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 8 - 14;
                }
                else
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 8 - 17;
                }

            }
            else if (temp <= 0x2000)
            {
                if (temp == 0x2000)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 6;
                }
                else if ((temp & 0x0FFF) < 0x0400)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 7 - 3;
                }
                else if ((temp & 0x0FFF) < 0x0700)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 7 - 6;
                }
                else if ((temp & 0x0FFF) < 0x0A00)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 7 - 10;
                }
                else if ((temp & 0x0FFF) < 0x0D00)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 7 - 14;
                }
                else
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 7 - 17;
                }


            }
            else if (temp <= 0x3000)
            {
                if (temp == 0x3000)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 5;
                }
                else if ((temp & 0x0FFF) < 0x0400)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 6 - 3;
                }
                else if ((temp & 0x0FFF) < 0x0700)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 6 - 6;
                }
                else if ((temp & 0x0FFF) < 0x0A00)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 6 - 10;
                }
                else if ((temp & 0x0FFF) < 0x0D00)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 6 - 14;
                }
                else
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 6 - 17;
                }

            }
            else if (temp <= 0x4000)
            {
                if (temp == 0x4000)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 4;
                }
                else if ((temp & 0x0FFF) < 0x0400)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 5 - 3;
                }
                else if ((temp & 0x0FFF) < 0x0700)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 5 - 6;
                }
                else if ((temp & 0x0FFF) < 0x0A00)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 5 - 10;
                }
                else if ((temp & 0x0FFF) < 0x0D00)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 5 - 14;
                }
                else
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 5 - 17;
                }

            }
            else if (temp <= 0x5000)
            {
                if (temp == 0x5000)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 3;
                }
                else if ((temp & 0x0FFF) < 0x0400)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 4 - 3;
                }
                else if ((temp & 0x0FFF) < 0x0700)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 4 - 6;
                }
                else if ((temp & 0x0FFF) < 0x0A00)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 4 - 10;
                }
                else if ((temp & 0x0FFF) < 0x0D00)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 4 - 14;
                }
                else
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 4 - 17;
                }

            }
            else if (temp <= 0x6000)
            {
                if (temp == 0x6000)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 2;
                }
                else if ((temp & 0x0FFF) < 0x0400)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 3 - 3;
                }
                else if ((temp & 0x0FFF) < 0x0700)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 3 - 6;
                }
                else if ((temp & 0x0FFF) < 0x0A00)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 3 - 10;
                }
                else if ((temp & 0x0FFF) < 0x0D00)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 3 - 14;
                }
                else
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 3 - 17;
                }

            }
            else if (temp <= 0x7000)
            {
                if (temp == 0x7000)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 1;
                }
                else if ((temp & 0x0FFF) < 0x0400)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 2 - 3;
                }
                else if ((temp & 0x0FFF) < 0x0700)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 2 - 6;
                }
                else if ((temp & 0x0FFF) < 0x0A00)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 2 - 10;
                }
                else if ((temp & 0x0FFF) < 0x0D00)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 2 - 14;
                }
                else
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 2 - 17;
                }

            }
            else if (temp < 0x8000)
            {
                if ((temp & 0x0FFF) < 0x0400)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 1 - 3;
                }
                else if ((temp & 0x0FFF) < 0x0700)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 1 - 6;
                }
                else if ((temp & 0x0FFF) < 0x0A00)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 1 - 10;
                }
                else if ((temp & 0x0FFF) < 0x0D00)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 1 - 14;
                }
                else
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 1 - 17;
                }

            }
            else if (temp == 0x8000)
            {
                Y = WorkSpaceVolStart + WorkSpaceVolInterval * 16;
            }
            else if (temp <= 0x9000)
            {
                if (temp == 0x9000)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 15;
                }
                else if ((temp & 0x0FFF) < 0x0400)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 16 - 3;
                }
                else if ((temp & 0x0FFF) < 0x0700)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 16 - 6;
                }
                else if ((temp & 0x0FFF) < 0x0A00)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 16 - 10;
                }
                else if ((temp & 0x0FFF) < 0x0D00)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 16 - 14;
                }
                else
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 16 - 17;
                }
            }
            else if (temp <= 0xA000)
            {
                if (temp == 0xA000)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 14;
                }
                else if ((temp & 0x0FFF) < 0x0400)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 15 - 3;
                }
                else if ((temp & 0x0FFF) < 0x0700)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 15 - 6;
                }
                else if ((temp & 0x0FFF) < 0x0A00)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 15 - 10;
                }
                else if ((temp & 0x0FFF) < 0x0D00)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 15 - 14;
                }
                else
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 15 - 17;
                }

            }
            else if (temp <= 0xB000)
            {
                if (temp == 0xB000)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 13;
                }
                else if ((temp & 0x0FFF) < 0x0400)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 14 - 3;
                }
                else if ((temp & 0x0FFF) < 0x0700)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 14 - 6;
                }
                else if ((temp & 0x0FFF) < 0x0A00)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 14 - 10;
                }
                else if ((temp & 0x0FFF) < 0x0D00)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 14 - 14;
                }
                else
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 14 - 17;
                }

            }
            else if (temp <= 0xC000)
            {
                if (temp == 0xC000)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 12;
                }
                else if ((temp & 0x0FFF) < 0x0400)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 13 - 3;
                }
                else if ((temp & 0x0FFF) < 0x0700)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 13 - 6;
                }
                else if ((temp & 0x0FFF) < 0x0A00)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 13 - 10;
                }
                else if ((temp & 0x0FFF) < 0x0D00)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 13 - 14;
                }
                else
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 13 - 17;
                }
            }
            else if (temp <= 0xD000)
            {
                if (temp == 0xD000)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 11;
                }
                else if ((temp & 0x0FFF) < 0x0400)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 12 - 3;
                }
                else if ((temp & 0x0FFF) < 0x0700)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 12 - 6;
                }
                else if ((temp & 0x0FFF) < 0x0A00)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 12 - 10;
                }
                else if ((temp & 0x0FFF) < 0x0D00)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 12 - 14;
                }
                else
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 12 - 17;
                }

            }
            else if (temp <= 0xE000)
            {
                if (temp == 0xE000)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 10;
                }
                else if ((temp & 0x0FFF) < 0x0400)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 11 - 3;
                }
                else if ((temp & 0x0FFF) < 0x0700)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 11 - 6;
                }
                else if ((temp & 0x0FFF) < 0x0A00)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 11 - 10;
                }
                else if ((temp & 0x0FFF) < 0x0D00)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 11 - 14;
                }
                else
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 11 - 17;
                }

            }
            else if (temp <= 0xF000)
            {
                if (temp == 0xF000)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 9;
                }
                else if ((temp & 0x0FFF) < 0x0400)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 10 - 3;
                }
                else if ((temp & 0x0FFF) < 0x0700)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 10 - 6;
                }
                else if ((temp & 0x0FFF) < 0x0A00)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 10 - 10;
                }
                else if ((temp & 0x0FFF) < 0x0D00)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 10 - 14;
                }
                else
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 10 - 17;
                }

            }
            else if (temp < 0xFFFF)
            {
                if ((temp & 0x0FFF) < 0x0400)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 9 - 3;
                }
                else if ((temp & 0x0FFF) < 0x0700)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 9 - 6;
                }
                else if ((temp & 0x0FFF) < 0x0A00)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 9 - 10;
                }
                else if ((temp & 0x0FFF) < 0x0D00)
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 9 - 14;
                }
                else
                {
                    Y = WorkSpaceVolStart + WorkSpaceVolInterval * 9 - 17;
                }
            }
            else
            {
                Y = WorkSpaceVolStart + WorkSpaceVolInterval * 8;
            }

            return Y;
        }
        /// <summary>
        /// 用户函数，发送信息 发送按钮调用
        /// </summary>
        public void SendMessage(byte[] Data)
        {
          _udpClient.Send(Data, Data.Length, _remoteEndPoint);           
        }
        //按 协议写报文  给定Ycal值，自动填充1号报文
        private void construct1(byte value)
        {
            LEDdata= constructDataBuffer(1, 0, 0, 0, value, value, value,0, 0, 0,0x7f, 0x7f, 0x7f, 1);
        }
        //按 协议写报文  给定R G B值，自动填充83号报文
        private void construct83(byte R, byte G, byte B)
        {
            LEDdata = constructDataBuffer(83, 0x0, 0x0, 0x0, R, G, B, 0x0, 0x0, 0x0, 0x7f, 0x7f, 0x7f, 1);
        }
        //按 协议写报文  给定Ycal值，自动填充1号报文
        private void construct2(byte value)
        {
            LEDdata = constructDataBuffer(1, value, value, value, 0x0, 0x0, 0x0, value, value, value, 0x7f, 0x7f, 0x7f, 1);
        }
        //按 协议写报文
        private byte[] constructDataBuffer(byte mode, byte R1, byte G1, byte B1, byte R2, byte G2, byte B2, byte R3, byte G3, byte B3, byte speed, byte density, byte other, byte number)
        {
            byte[] data = { 0x55, mode, R1, G1, B1, R2, G2, B2, R3, G3, B3, speed, density, other, number, 0, 0xf0 };
            byte checsum = checkdata(data);
            data[15] = checsum;
            return data;
        }
        //协议较检函数
        private byte checkdata(byte[] p)
        {
            byte checksum = 0;
            byte i;
            for (i = 1; i < 15; i++)
            {
                checksum ^= p[i];
            }
            return checksum;
        }
        private void button_openfile_Click(object sender, EventArgs e)
        {
            //文件打开对话框处理
            openFileDialog.InitialDirectory = ".";
            openFileDialog.Filter = "wav(*.wav)|*.wav|(*.*)|*.*";
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 0;
            openFileDialog.FileName = "";

            try
            {
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    FileStream fs = File.Open(openFileDialog.FileName, FileMode.Open);//文件流
                    wmp.URL = openFileDialog.FileName;//zc   打开媒体文件
                    wmp.controls.stop();//默认先暂停，不播放

                        waveFile = new byte[Convert.ToInt32(fs.Length)];
                        fs.Read(waveFile, 0, Convert.ToInt32(fs.Length));
                        wf = new WaveFile(waveFile);
                        wavData = wf.wData;
                        totalTime = (float)(wf.wData.Length / wf.dwAvgBytesRate);//数据长度/每秒所需字节数   =   wav文件总时长

                    //以上将数据都读取进去了 
                    num = (uint)(wf.wData.Length/wf.wBlockAlign);// 总共的样本个数  
                    samples_per_intertime = ((float)wf.dwAvgBytesRate * inter_time) / (1000 * (float)wf.wBlockAlign);
                    //10ms内样本的个数
                    num_samples = (uint)((float)num *100* (float)wf.wBlockAlign / (float)wf.dwAvgBytesRate);
                    
                    //处理点集
                    Datapoint = new Point[num];//装入点集   数据量大小为  wav文件中总样本大小
                 //   array_intersample = new ushort[num_samples];//数据量大小为  每个时间间隔内样本个数，现在默认的是10ms内，样本个数
                    uint temp = 0, index = 0;
                    for (uint i = 0; i < num; i++) // Parallel.For(0, num, i =>
                    {
                        index = (uint)i * wf.wBlockAlign;
                        temp = (uint)((wavData[index] & 0x00ff) | ((wavData[index + 1] & 0x00ff) << 8));//两个字节拼起来
                        Datapoint[i].X = (int)i;
                        Datapoint[i].Y = wavYCal(temp);//解码
                    }
                 /*   for (uint i = 0; i < num_samples; i++)//有多少个10ms，遍历多少个,相当于10ms周期栅格化
                    {
                        temp = (uint)Datapoint[(uint)((float)(i+1) * samples_per_intertime)].Y;
                        for (float j = 1; j < samples_per_intertime; j++)//10ms内有多少个样本
                        {//在这10ms内，需要记下最大的Y值，保存在array_intersample数组里
                            if (temp < (uint)Datapoint[(uint)((float)i * samples_per_intertime + j)].Y)
                                temp = (uint)Datapoint[(uint)((float)i * samples_per_intertime + j)].Y;
                        }
                        array_intersample[i] = (ushort)temp;
                    }*/
                    fs.Close();
                }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("OpenFile_Click error!");
                }
            if (this.lblTrb.InvokeRequired)
            {//使用委托来赋值
                this.lblTrb.Invoke(new EventHandler(delegate
                {
                    lblTrb.Left = 0;
                    lblTrb.Top = 0;
                    lblTrb.Height = WorkSpaceHeight;
                    lblTrb.Width = 2;
                    lblTrb.Show();
                }));//参数
            }
            else
            {
                lblTrb.Left = 0;
                lblTrb.Top = 0;
                lblTrb.Height = WorkSpaceHeight;
                lblTrb.Width = 2;
                lblTrb.Show();
            }

            //下面代码段处理 垂直、水平滚动条   保留以后扩展功能
            //1、计算垂直水平滚动条范围   决定着放大倍
            //固定最大只放大 0^5 大小 1-32倍
            vScrollBar_Value = beishu; vScrollBar_Maximum = beishu;
            if (this.vScrollBar.InvokeRequired)
            {//使用委托来赋值
                this.vScrollBar.Invoke(new EventHandler(delegate
                {
                    vScrollBar.Maximum = beishu;
                    vScrollBar.Value = beishu;
                }));//参数
            }
            else
            {
                vScrollBar.Maximum = beishu;
                vScrollBar.Value = beishu;
            }
            current_beishu_num = 1;//当前默认放大倍数为1  即全屏显示波形图          
            drawWav_zc();
            //DrawTimeScale();//水平刻度对齐
        }
  
            private void button_play_Click(object sender, EventArgs e)
            {
                wmp.controls.play();//播放
                playstatus = true;
                Data_array_index = last_Data_array_index;
                //st.Start();//启动秒表
               // miliseconds1=st.ElapsedMilliseconds;//记录秒表开始时刻
                trMediaTime.Start();
                try
                { //开启或关闭
                    if (playstatus)
                    {
                        Thread th = new Thread(this.SendLEDmessage);//启动发送LED报文的程序
                        th.IsBackground = true;//指定后台线程运行
                        th.Start();//线程启动
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "启动发送LED报文线程错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
        }
            /// <summary>
            /// 设置文本框文本
            /// </summary>
            /// <param name="txt"></param>
            /// <param name="Text"></param>
            public delegate void SetTextBoxTextDelegate(TextBox txt, string Text);
            //自定义委托
            public delegate void zhangcan(PictureBox picBox, Image image);
        /// <summary>
        /// 发送LED消息的线程，1ms周期 询问当前时间
        /// </summary>
            private void SendLEDmessage()//发送LED消息（UDP报文）
            {
           //   try
              //  {
                    while (playstatus)
                    {//读取时间
                     // miliseconds2 = st.ElapsedMilliseconds;
                     //  temp = already_passed_time+((float)(miliseconds2 - miliseconds1) / 1000.0f);
                try{
                    temp = wmp.controls.currentPosition;
                    }
                catch
                   {
                     continue;
                    }
                        if (Data_array_index<Data_array.Count)
                            if (temp > Data_array[Data_array_index].time)
                            {
                                //richTextBox_for_debug.AppendText(duilie.Peek().time.ToString() + "   ");
                                SendMessage(Data_array[Data_array_index].Led_data);
                            Data_array_index++;
                            }
                            Application.DoEvents(); //     处理当前在消息队列中的所有 Windows 消息。
                    }
             //   }
              //  catch (Exception ex)
               // {
                //    Console.WriteLine(ex.Message);
               // }
            }
            /// <summary>
            /// 显示时间
            /// </summary>
            private void ShowTime()
            {
                try
                {
                    while (playstatus)
                    {
                        //读取时间
                        string datetime = string.Format("{0}.{1}", DateTime.Now.ToLongTimeString(), DateTime.Now.Millisecond);
                        //设置文本显示
                        if (this.txtTime.InvokeRequired)
                        {//控件的 System.Windows.Forms.Control.Handle 是在与调用线程不同的线程上创建的（说明您必须通过 Invoke 方法对控件进行调用）
                            //使用委托来赋值
                            this.txtTime.Invoke(
                                //委托方法
                                new SetTextBoxTextDelegate(
                                (txt, text) => txt.Text = text //这里采用Lambda表达式来实现
                                ),
                                new object[] { this.txtTime, datetime });//参数
                        }
                        else
                        {
                            this.txtTime.Text = datetime;
                        }
                        Application.DoEvents(); //     处理当前在消息队列中的所有 Windows 消息。
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
            
            private void button_zanting_Click(object sender, EventArgs e)
            {
                wmp.controls.pause();//暂停
            last_Data_array_index=Data_array_index;
            playstatus = false;
                trMediaTime.Stop();//暂停
           }

            private void button_stop_Click(object sender, EventArgs e)
            {
                wmp.controls.stop();
                last_Data_array_index = 0;
               Data_array_index = 0;
               playstatus = false;
               // st.Stop();//停止秒表
                trMediaTime.Stop(); //停止 
            }

            private void button_backward_Click(object sender, EventArgs e)
            {
            float aaaaaa = 0; int Trb_left = 0;
                wmp.controls.fastReverse(); //快退  快退一格
                                            // 1、更新进度条位置
            aaaaaa = BackGround_interval_time * WavScale;
            temp = wmp.controls.currentPosition - BackGround_first_time;
            Trb_left = 10 + (int)((WorkSpaceWidth / aaaaaa) * temp);//wmp.controls.currentPosition 当前播放时间进度，s
            if (this.lblTrb.InvokeRequired)
            {//使用委托来赋值
                this.lblTrb.Invoke(new EventHandler(delegate
                {
                    lblTrb.Left = Trb_left;
                }));//参数
            }
            else
            {
                lblTrb.Left = Trb_left;
            }
            // 2、更新标定数组当前位置
            current_play_time = (float)temp+ BackGround_first_time;
            for (int i = 0; i < Data_array.Count; i++)
            {
                if (Data_array[i].time > current_play_time)
                {
                    Data_array_index = i;
                    break;
                }
            }
            last_Data_array_index = Data_array_index;
        }

        private void button_fastward_Click(object sender, EventArgs e)
            {
            float aaaaaa = 0;  int  Trb_left = 0;
            wmp.controls.fastForward(); //快进  快进一格
                                        //1、更新进度条位置
            aaaaaa = BackGround_interval_time * WavScale;
            temp = wmp.controls.currentPosition - BackGround_first_time;
            Trb_left = 10 + (int)((WorkSpaceWidth / aaaaaa) * temp);//wmp.controls.currentPosition 当前播放时间进度，s
            if (this.lblTrb.InvokeRequired)
            {//使用委托来赋值
                this.lblTrb.Invoke(new EventHandler(delegate
                {
                    lblTrb.Left = Trb_left;
                }));//参数
            }
            else
            {
                lblTrb.Left = Trb_left;
            }
            // 2、更新标定数组当前位置
            current_play_time = (float)temp + BackGround_first_time;
            for (int i = 0; i < Data_array.Count; i++)
            {
                if (Data_array[i].time > current_play_time)
                {
                    Data_array_index = i;
                    break;
                }
            }
            last_Data_array_index = Data_array_index;
        }

            private void button_sendUPD_Click(object sender, EventArgs e)
            {
                try
                {
                    string message = this.textBox_send.Text;
                    Byte[]  byteArray = System.Text.Encoding.Default.GetBytes(message);
                    if (string.IsNullOrEmpty(message))
                    {
                        MessageBox.Show("发送信息不能为空");
                    }
                    else
                    {
                        SendMessage(byteArray);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
            
            public void jindutiao_track()//进度条
        {
            float aaaaaa = 0; int Trb_left = 0;
            aaaaaa = BackGround_interval_time * WavScale;

                temp = wmp.controls.currentPosition - BackGround_first_time;

            Trb_left = 10 + (int)((WorkSpaceWidth / aaaaaa) * temp);//wmp.controls.currentPosition 当前播放时间进度，s
            if (this.lblTrb.InvokeRequired)
            {//使用委托来赋值
                this.lblTrb.Invoke(new EventHandler(delegate
                {
                    lblTrb.Left = Trb_left;
                }));//参数
            }
            else
            {
                lblTrb.Left = Trb_left;
            }
        }
            private void trMediaTime_Tick(object sender, EventArgs e)//进度条 100ms定时器执行代码
        { //  
            float aaaaaa = 0;int Trb_left = 0;float wmp_current_time = 0.0f;
            wmp_current_time = (float)wmp.controls.currentPosition;//wmp.controls.currentPosition 当前播放时间进度，s
            aaaaaa = BackGround_interval_time * WavScale;//当前整个背景能显示的时间长度
            temp = wmp_current_time - BackGround_first_time;
            if(temp>aaaaaa)//说明进度条已经不在背景图中显示了
            {                
                if (this.hScrollBar.InvokeRequired)
                {//使用委托来赋值
                    if (hScrollBar.Value < hScrollBar.Maximum)
                    { 
                        this.hScrollBar.Invoke(new EventHandler(delegate
                         {
                           hScrollBar.Value += 1;
                           }));//参数
                    }
                }
                else
                {
                    if (hScrollBar.Value < hScrollBar.Maximum)
                    { hScrollBar.Value += 1; }
                }
                temp = wmp_current_time - BackGround_first_time;
            }
            if (wmp_current_time > 0)
                Trb_left = 10 + (int)((WorkSpaceWidth / aaaaaa) * temp);
            else
                Trb_left = 10;
            if (this.lblTrb.InvokeRequired)
            {//使用委托来赋值
                this.lblTrb.Invoke(new EventHandler(delegate
                {
                    lblTrb.Left = Trb_left;
                    lblTrb.Show();
                }));//参数
            }
            else
            {
                lblTrb.Left = Trb_left;
                lblTrb.Show();
            }
            //设置播放进度自动跟随

            //以下测试用
            biaoding_LED_read();//先从空间中读取
            if (flag==true)
            { 
            if (i_index <= toend)
            {
                LEDdata = constructDataBuffer(LEDmode, R1, G1, B1, R2, G2, B2, R3, (byte)(i_index / 256), (byte)(i_index % 256), speed, density, light, led_number);
                    SendMessage(LEDdata);
                    //输出到textBox_receive中来
                    if (this.textBox_receive.InvokeRequired)
                    {//使用委托来取值  怕出问题
                        this.textBox_receive.Invoke(new EventHandler(delegate
                        {
                            textBox_receive.Text = Convert.ToString(i_index);
                        }));//参数
                    }
                    else
                    {
                        textBox_receive.Text = Convert.ToString(i_index);
                    }
                    i_index++;
                }
                else
                {
                    flag = false;
                }
            }
        }

            private void timer_send_Tick(object sender, EventArgs e)//定时器 10ms   执行代码
            {
                //这个定时器 10ms 运行一次  需要做的事情就是 10ms检查一次，看需不需要发送LED报文
                //wmp.controls.currentPosition 当前播放时间进度，s
                //4字节    波形数据传输速率（每秒平均字节数）每秒所需字节数 
                // num 总共的样本个数 
                //10ms
                //  if(wmp.playState== WMPLib.WMPPlayState.wmppsPlaying)////正在播放状态时
                // if (playstatus ==true)////正在播放状态时
            }

            private void txtRemoteHost_TextChanged(object sender, EventArgs e)
            {
                init();
            }

            private void txtRemotePort_TextChanged(object sender, EventArgs e)
            {
                init();
            }

            private void txtport_TextChanged(object sender, EventArgs e)
            {
                init();
            }

            private void button1_Click(object sender, EventArgs e)//发送UDP报文16进制  按钮响应函数
            {
                try
                {
                    string message = this.textBox_send_16.Text;//16进制字符串转 byte[]
                                                               //char[] kongge =｛32};
                    message = message.Replace(" ", "");//去掉空格
                    if ((message.Length % 2) != 0)
                        message += " ";
                    byte[] returnBytes = new byte[message.Length / 2];
                    for (int i = 0; i < returnBytes.Length; i++)
                        returnBytes[i] = Convert.ToByte(message.Substring(i * 2, 2), 16);

                    if (string.IsNullOrEmpty(message))
                    {
                        MessageBox.Show("发送信息不能为空");
                    }
                    else
                    {
                        SendMessage(returnBytes);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
            int i_index = 0; int toend = 0;bool flag = false;
            private void button_save_Click(object sender, EventArgs e)
            {//保存文件
             //测试发1000包
          
            i_index =  Convert.ToInt32(textBox_receive.Text); 
            flag = true;
            toend = Convert.ToInt32(textBox_m_zc2.Text);
            trMediaTime.Start();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void pictureBox_Click(object sender, EventArgs e)
        {//什么时候 执行？
         /*   if (this.textBox_receive.InvokeRequired)
            {//使用委托来取值 
                this.textBox_receive.Invoke(new EventHandler(delegate
                {
                    textBox_receive.Text ="112233445566";
                }));//参数
            }
            else
            {
                textBox_receive.Text = "112233445566";
            }*/
        }
        float current_play_time = 0;
        int MouseDoubleClick_value = 0;
        private void pictureBox_MouseDoubleClick(object sender, MouseEventArgs e)
        {//在图片框中点击鼠标   双击选择进度条位置
            //1、更新lblTrb进度条位置，更新媒体播放时间进度
            MouseDoubleClick_value = e.X;//e.X 代表离pictruebox左上角原点 横坐标距离
            current_play_time = BackGround_first_time + ((float)(MouseClick_value) / (float)WorkSpaceWidth) * BackGround_interval_time * WavScale;//这个算的是对的
            wmp.controls.currentPosition = current_play_time;   
            if (this.lblTrb.InvokeRequired)
            {//使用委托来赋值
                this.lblTrb.Invoke(new EventHandler(delegate
                {
                    lblTrb.Left = MouseDoubleClick_value + 10;
                }));//参数
            }
            else
            {
                
                lblTrb.Left = MouseDoubleClick_value + 10;
            }
            //2、更新Data_array_index 的值，这样每次都从当前播放处开始发送LED报文           
            for (int i =0; i < Data_array.Count ; i++)
            {
                if (Data_array[i].time > current_play_time)
                {
                    Data_array_index = i;
                    break;
                }
            }
            last_Data_array_index = Data_array_index;
            //3、更新已经经过的时间值 秒表重新开始计时
            already_passed_time = current_play_time;
            //4、重新关闭、开启秒表计时器，重新更新
           // st.Stop();//启动秒表
           // st.Start();//启动秒表
          //  miliseconds1 = st.ElapsedMilliseconds;//记录秒表开始时刻
            //以下测试用
           // textBox_receive.Text = Convert.ToString(Data_array_index);
        }

        private void pictureBox_DoubleClick(object sender, EventArgs e)
        {
            
        }

        private void groupBox_zc_Enter(object sender, EventArgs e)
        {

        }

        private void button1_Click_1(object sender, EventArgs e)//重新标定当前点    按钮响应函数
        {//1、查找label_time 正负zengliang_time范围内所有点，对原存在点进行删除
            for (int i = Data_array.Count - 1; i >= 0; i--)
            {
                if ((Data_array[i].time > (label_time - zengliang_time)) && (Data_array[i].time < (label_time + zengliang_time)))
                {
                    Data_array.RemoveAt(i);
                }
            }
            //2、新增新的点（当前label_time点）
            button_biaoding_Click(sender, e);
        }

        float label_time = 0;

        private void Form_zc_KeyDown(object sender, KeyEventArgs e)
        {
            /*  switch (e.KeyCode)
              {
                  case Keys.Enter:
                      MessageBox.Show("按下空格键切换暂停/播放");
                      break;
                  case Keys.Space:
                      if (playstatus == true)//当前处于播放状态，需要切换到暂停状态
                      {
                          button_play_Click(sender, e);
                      }
                      else//当前处于暂停/停止状态，需要切换到播放状态
                      {
                          button_zanting_Click(sender, e);
                      }
                      break;
                  default:
                      break;
              }*/
   
           
        }

        private void button_shutdown_led_Click(object sender, EventArgs e)
        {//测试用灭灯动作   就是发模式为0的报文
            SendMessage(constructDataBuffer(0, 0, 0,0, 0, 0, 0, 0, 0,0, 0, 0, 0, 1));
        }

        private void button_play_KeyDown(object sender, KeyEventArgs e)
        {

        }

        private void Form_zc_KeyPress(object sender, KeyPressEventArgs e)
        {
          /*  switch (e.KeyChar)
            {
                case (char)(13):
                    MessageBox.Show("按下空格键切换暂停/播放");
                    break;
                case (char)(32):
                    if (playstatus == true)//当前处于播放状态，需要切换到暂停状态
                    {
                        button_play_Click(sender, e);
                    }
                    else//当前处于暂停/停止状态，需要切换到播放状态
                    {
                        button_zanting_Click(sender, e);
                    }
                    break;
                default:
                    break;
            }*/
        }

        int MouseClick_value = 0;

        private void textBox_zengliang_time_TextChanged(object sender, EventArgs e)
        {
            zengliang_time = Convert.ToSingle(textBox_zengliang_time.Text);
            drawWav_zc();
        }

        private void radioButton_colorDialog_CheckedChanged(object sender, EventArgs e)
        {
       
           
        }
        
        private void button_colorDialog_1_Click(object sender, EventArgs e)
        {
            if (colorDialog_zc.ShowDialog() == DialogResult.OK)
            {
                R1=colorDialog_zc.Color.R;
                G1= colorDialog_zc.Color.G;
                B1 = colorDialog_zc.Color.B;
                tb_R1_1.Text = Convert.ToString(R1);
                tb_G1_1.Text = Convert.ToString(G1);
                tb_B1_1.Text = Convert.ToString(B1);
                label_C0.BackColor = colorDialog_zc.Color;
            }
        }

        private void button_colorDialog2_Click(object sender, EventArgs e)
        {
            if (colorDialog_zc.ShowDialog() == DialogResult.OK)
            {
                R2 = colorDialog_zc.Color.R;
                G2 = colorDialog_zc.Color.G;
                B2 = colorDialog_zc.Color.B;
                tb_R2_1.Text = Convert.ToString(R2);
                tb_G2_1.Text = Convert.ToString(G2);
                tb_B2_1.Text = Convert.ToString(B2);
                label_C1.BackColor = colorDialog_zc.Color;
            }
        }

        private void button_colorDialog3_Click(object sender, EventArgs e)
        {
            if (colorDialog_zc.ShowDialog() == DialogResult.OK)
            {
                R3 = colorDialog_zc.Color.R;
                G3 = colorDialog_zc.Color.G;
                B3 = colorDialog_zc.Color.B;
                tb_R3_1.Text = Convert.ToString(R3);
                tb_G3_1.Text = Convert.ToString(G3);
                tb_B3_1.Text = Convert.ToString(B3);
                label_C2.BackColor = colorDialog_zc.Color;
            }
        }

        private void trackBar_Speed_Scroll(object sender, EventArgs e)
        {
            tb_Speed1.Text= Convert.ToString(trackBar_Speed.Value);
        }

        private void trackBar_density_Scroll(object sender, EventArgs e)
        {
            tb_Density1.Text= Convert.ToString(trackBar_density.Value);
        }

        private void trackBar_light_Scroll(object sender, EventArgs e)
        {
            tb_Other1.Text= Convert.ToString(trackBar_light.Value);
        }

        private void txtTime_TextChanged(object sender, EventArgs e)
        {
          /* 
            */
        }

        float zengliang_time = 0.2f;

        private void button_goto_Click(object sender, EventArgs e)
        {
            //用户自己输入label_time时间值
            int zengliang = 0;
            label_time = Convert.ToSingle(txtTime.Text);
            //由label_time 反算MouseClick_value的值
            MouseClick_value = (int)(((label_time - BackGround_first_time) * (float)WorkSpaceWidth) / (BackGround_interval_time * WavScale));
            zengliang = (int)((float)WorkSpaceWidth * (zengliang_time) / (BackGround_interval_time * WavScale));
            if (this.label_zc.InvokeRequired)//1、对 绿色标记条 位置进行重新赋值
            {//使用委托来赋值
                this.label_zc.Invoke(new EventHandler(delegate
                {
                    label_zc.Left = MouseClick_value + 10;//
                    label_zc.Show();
                }));//参数
            }
            else
            {
                label_zc.Left = MouseClick_value + 10;//
                label_zc.Show();
            }
            /////////////////////////2、对 绿色标记条 左边小条 位置进行重新赋值
            if (this.label_zc_left.InvokeRequired)//对 绿色标记条 左边小条 位置进行重新赋值
            {//使用委托来赋值
                this.label_zc_left.Invoke(new EventHandler(delegate
                {
                    label_zc_left.Left = MouseClick_value + 10 - zengliang;//
                    label_zc_left.Show();
                }));//参数
            }
            else
            {
                label_zc_left.Left = MouseClick_value + 10 - zengliang;//
                label_zc_left.Show();
            }
            /////////////////////////////3、对 绿色标记条 右边小条 位置进行重新赋值
            if (this.label_zc_right.InvokeRequired)//对 绿色标记条 右边小条 位置进行重新赋值
            {//使用委托来赋值
                this.label_zc_right.Invoke(new EventHandler(delegate
                {
                    label_zc_right.Left = MouseClick_value + 10 + zengliang;//
                    label_zc_right.Show();
                }));//参数
            }
            else
            {
                label_zc_right.Left = MouseClick_value + 10 + zengliang;//
                label_zc_right.Show();
            }

            //现在加的一个新需求，就是点中那个点，选区内标定点信息显示
            for (int i = Data_array.Count - 1; i >= 0; i--)
            {
                if ((Data_array[i].time > (label_time - zengliang_time)) && (Data_array[i].time < (label_time + zengliang_time)))
                {
                    biaoding_LED_display(Data_array[i].time, Data_array[i].Led_data, Data_array[i].explain);
                }
            }
        }

        private void bt_change_num_Click(object sender, EventArgs e)
        {
            biaoding_LED_read();
            //  led_number
            for (int i = 0; i< Data_array.Count; i++)
            {
                Data_array[i].Led_data[14] = led_number;
                Data_array[i].Led_data[15] = checkdata(Data_array[i].Led_data);
            }

        }

        private void button_load_time_Click(object sender, EventArgs e)
        {    //保存所有标定点 时间信息到txt文件
            String tempdata="";
            //1、将标定点数组内容  转成string
            if (Data_array.Count > 0)//有数据的情况下
            {               
                foreach (MyDatastruct item in Data_array)
                {
                    tempdata = tempdata + item.time.ToString("0.0000") + "  "+ item.explain+Environment.NewLine;
                    //tempdata = tempdata + string.Format("%4f  /n", item.time);
                }
            }
            else
            {
                tempdata = "";
            }

            //1、将标定点数组时间内容  写到txt文件 
            string localFilePath = "";//string localFilePath, fileNameExt, newFileName, FilePath; 
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "（*.txt）|*.txt"; //设置文件类型           
            sfd.FilterIndex = 1; //设置默认文件类型显示顺序             
            sfd.RestoreDirectory = true;//保存对话框是否记忆上次打开的目录 
            if (sfd.ShowDialog() == DialogResult.OK)  //点了保存按钮进入 
            {
                localFilePath = sfd.FileName.ToString(); //获得文件路径 
                string fileNameExt = localFilePath.Substring(localFilePath.LastIndexOf("\\") + 1); //获取文件名，不带路径               
                //FilePath = localFilePath.Substring(0, localFilePath.LastIndexOf("\\")); //获取文件路径，不带文件名               
                //newFileName = DateTime.Now.ToString("yyyyMMdd") + fileNameExt;  //给文件名前加上时间              
                //saveFileDialog1.FileName.Insert(1,"dameng");    //在文件名里加字符 
                //System.IO.FileStream fs = (System.IO.FileStream)sfd.OpenFile();//输出文件 
                using (FileStream fs = new FileStream(localFilePath, FileMode.OpenOrCreate))
                {
                    BinaryWriter bf = new BinaryWriter(fs);
                    byte[] data = System.Text.Encoding.Default.GetBytes(tempdata);
                    fs.Write(data, 0, data.Length);
                    bf.Close();
                    bf.Dispose();
                }
            }
        }

        private void pictureBox_MouseClick(object sender, MouseEventArgs e)//在图片框中单击鼠标
        {//在图片框中点击鼠标  单击选择  某个点  在某个范围内识别原先标定点   
            int zengliang = 0;//用来表示zengliang_times的增量下，代表多少个像素点。
            MouseClick_value = e.X;//e.X 代表离pictruebox左上角原点 横坐标距离
            label_time = BackGround_first_time + ((float)(MouseClick_value) / (float)WorkSpaceWidth) * BackGround_interval_time * WavScale;//这个算的是对的
            zengliang = (int)((float)WorkSpaceWidth * (zengliang_time) / (BackGround_interval_time * WavScale));
            if (this.label_zc.InvokeRequired)//1、对 绿色标记条 位置进行重新赋值
            {//使用委托来赋值
                this.label_zc.Invoke(new EventHandler(delegate
                {
                    label_zc.Left = MouseClick_value+10;//
                    label_zc.Show();
                }));//参数
            }
            else
            {
                label_zc.Left = MouseClick_value + 10;//
                label_zc.Show(); 
            }
            /////////////////////////2、对 绿色标记条 左边小条 位置进行重新赋值
            if (this.label_zc_left.InvokeRequired)//对 绿色标记条 左边小条 位置进行重新赋值
            {//使用委托来赋值
                this.label_zc_left.Invoke(new EventHandler(delegate
                {
                    label_zc_left.Left = MouseClick_value + 10-zengliang;//
                    label_zc_left.Show();
                }));//参数
            }
            else
            {
                label_zc_left.Left = MouseClick_value + 10- zengliang;//
                label_zc_left.Show();
            }
            /////////////////////////////3、对 绿色标记条 右边小条 位置进行重新赋值
            if (this.label_zc_right.InvokeRequired)//对 绿色标记条 右边小条 位置进行重新赋值
            {//使用委托来赋值
                this.label_zc_right.Invoke(new EventHandler(delegate
                {
                    label_zc_right.Left = MouseClick_value + 10+ zengliang;//
                    label_zc_right.Show();
                }));//参数
            }
            else
            {
                label_zc_right.Left = MouseClick_value + 10+ zengliang;//
                label_zc_right.Show();
            }
            if (this.txtTime.InvokeRequired)//  4、 取的当前时间  输出到txtTime文本框中来
            {//使用委托来赋值
                this.txtTime.Invoke(new EventHandler(delegate
                {
                    txtTime.Text= string.Format("{0,-3}", label_time.ToString("f3"));
                }));//参数
            }
            else
            {
                txtTime.Text= string.Format("{0,-3}", label_time.ToString("f3"));
            }
            //5、现在加的一个新需求，就是点中那个点，选区内标定点信息显示
            bool find = false;
            for (int i = Data_array.Count - 1; i >= 0; i--)
            {
                if ((Data_array[i].time > (label_time - zengliang_time)) && (Data_array[i].time < (label_time + zengliang_time))) 
                {
                    biaoding_LED_display(Data_array[i].time, Data_array[i].Led_data, Data_array[i].explain);
                    find = true;
                }
            }
            if (find == false)
            {
                textBox_explain.Text = "";
            }
        }

        private void vScrollBar_Scroll(object sender, ScrollEventArgs e)
        {

        }

        private void hScrollBar_ValueChanged(object sender, EventArgs e)
        {
            float temp_num;
            hScrollBar_Value = hScrollBar.Value;
            temp_num = (float)num / (float)current_beishu_num;//算出每个放大了屏 要显示多少个样本
            cal_background();
            if (hScrollBar_Value % 2 == 0)//偶数的情况 0 2 4 6 8等等
            {
                num_start = (uint)((hScrollBar_Value / 2) * temp_num);
                num_end = num_start + (uint)temp_num;
            }
            else//奇数的情况1 3 5 7 9
            {
                num_start = (uint)((uint)(hScrollBar_Value / 2) * temp_num) + (uint)(temp_num / 2);
                num_end = num_start + (uint)temp_num;
            }
            if (num_end > num) num_end = num;


            button_goto_Click(sender, e);

            drawWav_zc();
            TimeScaleValue();//水平时间内容重写
            //以下代码测试用
         /*   //显示backgroud_first_time 和 backgroud_end_time 
            if (this.textBox_receive.InvokeRequired)
            {//使用委托来取值 
                this.textBox_receive.Invoke(new EventHandler(delegate
                {
                    textBox_receive.Text = string.Format("{0,-3}", BackGround_first_time.ToString("f3")) + "  " + string.Format("{0,-3}", BackGround_end_time.ToString("f3"));
                }));//参数
            }
            else
            {
                textBox_receive.Text = string.Format("{0,-3}", BackGround_first_time.ToString("f3")) + "  " + string.Format("{0,-3}", BackGround_end_time.ToString("f3"));
            }
           */
        }
        uint current_beishu_num = 1;

        private void button_cansel_Click(object sender, EventArgs e)
        {//取消 标定点
            //获取当前MouseClick   代表的label_time，在此时间基础上，正负zengliang_time范围，查找Data_array数组，
            //删除数组中  time在这个范围内的元素
            for (int i= Data_array.Count-1; i>=0 ;i--)
            {
                if((Data_array[i].time> (label_time- zengliang_time))&& (Data_array[i].time < (label_time + zengliang_time)))
                {
                    Data_array.RemoveAt(i);
                }
            }
            drawWav_zc();
        }

        private void button_save_to_file_Click(object sender, EventArgs e)
        {//保存所有标定点 到文件
            byte[] tempdata;int xiabiao = 0;
                //1、将标定点数组内容  转成字节流
                if (Data_array.Count > 0)//有数据的情况下
            {
                tempdata = new byte[Data_array.Count * 21];//字节流数组，21由来是  time 4个字节，LED报文 17个字节
                foreach (MyDatastruct item in Data_array)
                {
                    //BitConverter.GetBytes(item.time);  深度拷贝
                    Buffer.BlockCopy(BitConverter.GetBytes(item.time), 0, tempdata, xiabiao, 4);
                    xiabiao += 4;
                    Buffer.BlockCopy(item.Led_data, 0, tempdata, xiabiao, 17);
                    xiabiao += 17;
                }
            }
            else
            {
                tempdata = new byte[]{ 1, 2, 3, 4, 5, 6, 7, 8, 9, 0 };
            } 
            //2、将标定点数组内容  写到文件 
            string localFilePath = "";//string localFilePath, fileNameExt, newFileName, FilePath; 
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "（*.dat）|*.dat"; //设置文件类型           
            sfd.FilterIndex = 1; //设置默认文件类型显示顺序             
            sfd.RestoreDirectory = true;//保存对话框是否记忆上次打开的目录 
            if (sfd.ShowDialog() == DialogResult.OK)  //点了保存按钮进入 
            {
                localFilePath = sfd.FileName.ToString(); //获得文件路径 
                string fileNameExt = localFilePath.Substring(localFilePath.LastIndexOf("\\") + 1); //获取文件名，不带路径               
                //FilePath = localFilePath.Substring(0, localFilePath.LastIndexOf("\\")); //获取文件路径，不带文件名               
                //newFileName = DateTime.Now.ToString("yyyyMMdd") + fileNameExt;  //给文件名前加上时间              
                //saveFileDialog1.FileName.Insert(1,"dameng");    //在文件名里加字符 
                //System.IO.FileStream fs = (System.IO.FileStream)sfd.OpenFile();//输出文件 
                using (FileStream fs = new FileStream(localFilePath, FileMode.OpenOrCreate))
                {
                    BinaryWriter bf = new BinaryWriter(fs);
                    fs.Write(tempdata, 0, tempdata.Length);
                    bf.Close();
                    bf.Dispose();
                }
            }
            tempdata = null;
        }

        private void button_biaoding_Click(object sender, EventArgs e)//对当前点 进行标定
        {//对当前点 进行标定
            //当前点时间为 label_time
            byte[] temp_byte_array ;MyDatastruct temp_data;
            biaoding_LED_read();
            if(LEDmode==83)
            { 
            temp_byte_array =constructDataBuffer(83, 0xff, 0xff, 0xff, R2, G2, B2, 0xff, 0xff, 0xff, 0x0, 0xff, 0xff, led_number);
                temp_data = new MyDatastruct(label_time, temp_byte_array);
                temp_data.explain = jieshi;//
                Data_array.Add(temp_data);
            }
            else if(LEDmode == 2)
            {
                temp_byte_array = constructDataBuffer(1, R1, G1, B1, R2, G2, B2, R3, G3, B3, speed, density, light, led_number);
                temp_data = new MyDatastruct(label_time, temp_byte_array);
                temp_data.explain = jieshi;//
                Data_array.Add(temp_data);
                temp_byte_array = constructDataBuffer(0, R1, G1, B1, R2, G2, B2, R3, G3, B3, speed, density, light, led_number);
                temp_data = new MyDatastruct((float)(label_time+0.1), temp_byte_array);
                temp_data.explain = jieshi;//
                Data_array.Add(temp_data);
            }
            else 
            {
            temp_byte_array = constructDataBuffer(LEDmode, R1, G1, B1, R2, G2, B2, R3, G3, B3, speed, density, light, led_number);
                temp_data = new MyDatastruct(label_time, temp_byte_array);
                temp_data.explain = jieshi;//
                Data_array.Add(temp_data);
            }
            //将标定点  按时间大小排序   Data_array 数组排序   每新增一个点，都要重新排序，确保每时每刻都是有序的
            if (Data_array.Count > 0)//有数据的情况下 进行排序
            {
                Data_array.Sort(delegate (MyDatastruct x, MyDatastruct y)
                {
                    return x.time.CompareTo(y.time);//按时间大小 升序排序
                });
            }
            //以16进制模式 输出到text_message文本框中来
           // string text_message = string.Format("{0,-3}", label_time.ToString("f3"))+" ";  时间已经在 pictruebox_mouseclick响应中输出到文本框
            //byte[]转16进制字符串   并输出到textBox_send_16
            string Str = "";
            if (temp_byte_array != null)
            {
                for (int i = 0; i < temp_byte_array.Length; i++)
                {
                    Str += temp_byte_array[i].ToString("X2")+" ";
                }
            }
            if (this.textBox_send_16.InvokeRequired)
            {//使用委托来取值 
                this.textBox_send_16.Invoke(new EventHandler(delegate
                {
                    textBox_send_16.Text = Str;
                }));//参数
            }
            else
            {
                textBox_send_16.Text = Str;
            }
            //输出当前模式
            show_current_mode(LEDmode);
            //重绘背景图
            drawWav_zc();
        }
        public void show_current_mode(byte mode)
        {
            string Str = "";
            switch(mode)
            {
                case 0:
                    Str = "灭灯模式";
                    break;
                case 1:
                    Str = "亮单色：背景色(C1)";
                    break;
                case 11:
                    Str = "前景色C0，背景色C1交替闪亮";
                    break;
                case 12:
                    Str = "一个前景色C0及两个背景色C1交替闪亮";
                    break;
                case 41:
                    Str = "前景色C0在背景色C1上的跑马灯";
                    break;
                case 81:
                    Str = "背景色C1，辅助色C2交替渐变";
                    break;
                case 83:
                    Str = "背景色C1渐弱";
                    break;
                case 84:
                    Str = "前景色C0在背景色C1上闪一次";
                    break;
                case 121:
                    Str = "彩虹流水渐变";
                    break;
                case 161:
                    Str = "全彩随机闪烁";
                    break;
                case 162:
                    Str = "背景色C1上的全彩灯闪烁";
                    break;
                case 163:
                    Str = "前景色C0在背景色C1上随机闪";
                    break;
                case 255:
                    Str = "上电指示模式（3s）";
                    break;
                default:
                    Str = "未知模式";
                    break;
            }
            if (this.textBox_m_zc.InvokeRequired)
            {//使用委托来取值 
                this.textBox_m_zc.Invoke(new EventHandler(delegate
                {
                    textBox_m_zc.Text = Str;
                }));//参数
            }
            else
            {
                textBox_m_zc.Text = Str;
            }
        }
        private void button_open_biaoding_Click(object sender, EventArgs e)
        {//1、打开标定文件  .dat 文件
            byte[] byteFile;int bytelen=0,datalen = 0; MyDatastruct item; int xiabiao = 0;
            float tempfloat = 0.0f;byte[] tempLED;
            if (Data_array.Count > 0)//在Data_array有数据的情况下，先清除所有数据
            {
                Data_array.Clear();
            }
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "*.dat|*.dat";
                ofd.RestoreDirectory = true;//保存对话框是否记忆上次打开的目录 
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    using (FileStream fs = new FileStream(ofd.FileName, FileMode.Open))
                    {
                        // 
                        bytelen = Convert.ToInt32(fs.Length);
                        byteFile = new byte[bytelen];
                        fs.Read(byteFile, 0, bytelen);
                    }
                }
                else
                {
                    byteFile = new byte[]{ 1, 2, 3, 4, 5, 6, 7, 8, 9, 0 };
                }
            }
            //2、将字节流数据 存入到Data_array数组中来
           if(Convert.ToInt32(bytelen) %21==0)
            {
                datalen = bytelen / 21;
            }
           else
            {
                datalen = (bytelen / 21) + 1;//以防万一出错
            }
           for(int i=0;i< datalen; i++)//存入到Data_array数组
            {
                tempfloat=(float)BitConverter.ToSingle(byteFile, xiabiao);
                xiabiao = xiabiao + 4;
                tempLED = new byte[17];
                Buffer.BlockCopy(byteFile, xiabiao, tempLED, 0, 17);
                xiabiao = xiabiao + 17;
                item = new MyDatastruct(tempfloat, tempLED);
                Data_array.Add(item);
                  //以下测试是否可以正常读取标定文件     读取后再richTextBox_for_debug中显示读取的内容
                /*   string Str = ""; // byte[]转16进制字符串
                   if (item.Led_data != null)
                   {
                       for (int b = 0; b < item.Led_data.Length;b++)
                       {
                           Str += item.Led_data[b].ToString("X2");
                       }
                   }
                   richTextBox_for_debug.AppendText(item.time.ToString()+"   "+Str+"\n");  
                   */
            }
            //3、更新 背景图片内容
            drawWav_zc(); 
            //以下代码测试用            
            /*  if (this.textBox_receive.InvokeRequired)
              {//使用委托来取值 
                  this.textBox_receive.Invoke(new EventHandler(delegate
                  {
                      textBox_receive.Text = Convert.ToString(datalen);
                  }));//参数
              }
              else
              {
                  textBox_receive.Text = Convert.ToString(datalen);
                  //textBox_receive.Text = Convert.ToString(start_index) + "  " + Convert.ToString(end_index);
                  // textBox_receive.Text = string.Format("{0,-3}", BackGround_first_time.ToString("f3")) + "  " + string.Format("{0,-3}", BackGround_end_time.ToString("f3"));
              }
              */
        }
        public void biaoding_LED_display(float time,byte[] led,string str)
        {//从ledData数组中，更新R1 G1 B1 R2 G2 B2 R3 G3 B3 LEDmode speed density light LEDnumber值，并输出到相应控件中来
            LEDmode = led[1]; R1 = led[2]; G1 = led[3]; B1 = led[4];
            R2 = led[5]; G2 = led[6]; B2 = led[7];
            R3 = led[8]; G3 = led[9]; B3 = led[10];
            speed = led[11]; density = led[12]; light = led[13]; led_number = led[14];
            show_current_mode(LEDmode);//输出当前模式
            //输出C0 C1 C2颜色指示
            Color color_temp= new Color();
            color_temp=Color.FromArgb(R1, G1, B1);
            if (this.label_C0.InvokeRequired)
            {//使用委托来取值  怕出问题
                this.label_C0.Invoke(new EventHandler(delegate
                {
                    label_C0.BackColor = color_temp;
                }));//参数
            }
            else
            {
                label_C0.BackColor = color_temp;
            }
            color_temp = Color.FromArgb(R2, G2, B2);
            if (this.label_C1.InvokeRequired)
            {//使用委托来取值  怕出问题
                this.label_C1.Invoke(new EventHandler(delegate
                {
                    label_C1.BackColor = color_temp;
                }));//参数
            }
            else
            {
                label_C1.BackColor = color_temp;
            }
            color_temp = Color.FromArgb(R3, G3, B3);
            if (this.label_C2.InvokeRequired)
            {//使用委托来取值  怕出问题
                this.label_C2.Invoke(new EventHandler(delegate
                {
                    label_C2.BackColor = color_temp;
                }));//参数
            }
            else
            {
                label_C2.BackColor = color_temp;
            }
            /////////////////////////
            if (this.txtTime.InvokeRequired)
            {//使用委托来取值  怕出问题
                this.txtTime.Invoke(new EventHandler(delegate
                {
                    txtTime.Text = string.Format("{0,-3}", time.ToString("f3"));
                }));//参数
            }
            else
            {
                txtTime.Text = string.Format("{0,-3}", time.ToString("f3"));
            }
            ////////////////////////////////////更新R1 G1 B1
            if (this.tb_R1_1.InvokeRequired)
            {//使用委托来取值  怕出问题
                this.tb_R1_1.Invoke(new EventHandler(delegate
                {
                    tb_R1_1.Text = Convert.ToString(R1);
                }));//参数
            }
            else
            {
                tb_R1_1.Text = Convert.ToString(R1);
            }

            if (this.tb_G1_1.InvokeRequired)
            {//使用委托来取值  怕出问题
                this.tb_G1_1.Invoke(new EventHandler(delegate
                {
                   tb_G1_1.Text= Convert.ToString(G1);
                }));//参数
            }
            else
            {
                tb_G1_1.Text = Convert.ToString(G1);
            }

            if (this.tb_B1_1.InvokeRequired)
            {//使用委托来取值  怕出问题
                this.tb_B1_1.Invoke(new EventHandler(delegate
                {
                    tb_B1_1.Text = Convert.ToString(B1);
                }));//参数
            }
            else
            {
                tb_B1_1.Text = Convert.ToString(B1);
            }
            ////////////////////////////////////更新R2 G2 B2
            if (this.tb_R2_1.InvokeRequired)
            {//使用委托来取值  怕出问题
                this.tb_R2_1.Invoke(new EventHandler(delegate
                {
                    tb_R2_1.Text = Convert.ToString(R2);
                }));//参数
            }
            else
            {
                tb_R2_1.Text = Convert.ToString(R2);
            }

            if (this.tb_G2_1.InvokeRequired)
            {//使用委托来取值  怕出问题
                this.tb_G2_1.Invoke(new EventHandler(delegate
                {
                  tb_G2_1.Text= Convert.ToString(G2);
                }));//参数
            }
            else
            {
                tb_G2_1.Text = Convert.ToString(G2);
            }

            if (this.tb_B2_1.InvokeRequired)
            {//使用委托来取值  怕出问题
                this.tb_B2_1.Invoke(new EventHandler(delegate
                {
                  tb_B2_1.Text= Convert.ToString(B2);
                }));//参数
            }
            else
            {
                tb_B2_1.Text = Convert.ToString(B2);
            }
            ////////////////////////////////////更新R3 G3 B3
            if (this.tb_R3_1.InvokeRequired)
            {//使用委托来取值  怕出问题
                this.tb_R3_1.Invoke(new EventHandler(delegate
                {
                    tb_R3_1.Text = Convert.ToString(R3);
                }));//参数
            }
            else
            {
                tb_R3_1.Text = Convert.ToString(R3);
            }

            if (this.tb_G3_1.InvokeRequired)
            {//使用委托来取值  怕出问题
                this.tb_G3_1.Invoke(new EventHandler(delegate
                {
                   tb_G3_1.Text = Convert.ToString(G3);
                }));//参数
            }
            else
            {
                tb_G3_1.Text = Convert.ToString(G3);
            }

            if (this.tb_B3_1.InvokeRequired)
            {//使用委托来取值  怕出问题
                this.tb_B3_1.Invoke(new EventHandler(delegate
                {
                   tb_B3_1.Text= Convert.ToString(B3);
                }));//参数
            }
            else
            {
                tb_B3_1.Text = Convert.ToString(B3);
            }
            ////////////////////////////////////更新LEDmode speed density light LEDnumber
            if (this.tb_mod_1.InvokeRequired)
            {//使用委托来取值  怕出问题
                this.tb_mod_1.Invoke(new EventHandler(delegate
                {
                   tb_mod_1.Text = Convert.ToString(LEDmode );
                }));//参数
            }
            else
            {
                tb_mod_1.Text = Convert.ToString(LEDmode);
            }
            if (this.tb_Speed1.InvokeRequired)
            {//使用委托来取值  怕出问题
                this.tb_Speed1.Invoke(new EventHandler(delegate
                {
                    tb_Speed1.Text = Convert.ToString(speed);
                }));//参数
            }
            else
            {
                tb_Speed1.Text = Convert.ToString(speed);
            }
            if (this.tb_Density1.InvokeRequired)
            {//使用委托来取值  怕出问题
                this.tb_Density1.Invoke(new EventHandler(delegate
                {
                   tb_Density1.Text= Convert.ToString(density);
                }));//参数
            }
            else
            {
                tb_Density1.Text = Convert.ToString(density);
            }
            if (this.tb_Other1.InvokeRequired)
            {//使用委托来取值  怕出问题
                this.tb_Other1.Invoke(new EventHandler(delegate
                {
                   tb_Other1.Text  = Convert.ToString(light);
                }));//参数
            }
            else
            {
                tb_Other1.Text = Convert.ToString(light);
            }
            if (this.tb_Num1.InvokeRequired)
            {//使用委托来取值  怕出问题
                this.tb_Num1.Invoke(new EventHandler(delegate
                {
                    tb_Num1.Text = Convert.ToString(led_number);
                }));//参数
            }
            else
            {
                tb_Num1.Text = Convert.ToString(led_number);
            }

            if (this.textBox_explain.InvokeRequired)
            {//使用委托来取值  怕出问题
                this.textBox_explain.Invoke(new EventHandler(delegate
                {
                    textBox_explain.Text =str;
                }));//参数
            }
            else
            {
                textBox_explain.Text =str;
            }
            ////////////////////////2、将ledData数组内容，转成16进制字符串，输出到textBox_send_16
            //byte[]转16进制字符串   并输出到textBox_send_16
            string Str = "";
            if (led != null)
            {
                for (int i = 0; i < led.Length; i++)
                {
                    Str += led[i].ToString("X2") + " ";
                }
            }
            if (this.textBox_send_16.InvokeRequired)
            {//使用委托来取值 
                this.textBox_send_16.Invoke(new EventHandler(delegate
                {
                    textBox_send_16.Text = Str;
                }));//参数
            }
            else
            {
                textBox_send_16.Text =Str;
            }
            ////////////////////////
        }
        public void biaoding_LED_read()
        {//从控件中，更新R1 G1 B1 R2 G2 B2 R3 G3 B3 LEDmode speed density light LEDnumber值
            
            if (this.textBox_explain.InvokeRequired)
            {//使用委托来取值  怕出问题
                this.textBox_explain.Invoke(new EventHandler(delegate
                {
                    jieshi =textBox_explain.Text;
                }));//参数
            }
            else
            {
                jieshi = textBox_explain.Text;
            }
            ////////////////////////////////////更新R1 G1 B1
            if (this.tb_R1_1.InvokeRequired)
            {//使用委托来取值  怕出问题
                this.tb_R1_1.Invoke(new EventHandler(delegate
                {
                    R1 = Convert.ToByte(tb_R1_1.Text);
                }));//参数
            }
            else
            {
                R1= Convert.ToByte(tb_R1_1.Text);
            }

            if (this.tb_G1_1.InvokeRequired)
            {//使用委托来取值  怕出问题
                this.tb_G1_1.Invoke(new EventHandler(delegate
                {
                    G1 = Convert.ToByte(tb_G1_1.Text);
                }));//参数
            }
            else
            {
                G1 = Convert.ToByte(tb_G1_1.Text);
            }

            if (this.tb_B1_1.InvokeRequired)
            {//使用委托来取值  怕出问题
                this.tb_B1_1.Invoke(new EventHandler(delegate
                {
                    B1 = Convert.ToByte(tb_B1_1.Text);
                }));//参数
            }
            else
            {
                B1 = Convert.ToByte(tb_B1_1.Text);
            }
            ////////////////////////////////////更新R2 G2 B2
            if (this.tb_R2_1.InvokeRequired)
            {//使用委托来取值  怕出问题
                this.tb_R2_1.Invoke(new EventHandler(delegate
                {
                    R2 = Convert.ToByte(tb_R2_1.Text);
                }));//参数
            }
            else
            {
                R2 = Convert.ToByte(tb_R2_1.Text);
            }

            if (this.tb_G2_1.InvokeRequired)
            {//使用委托来取值  怕出问题
                this.tb_G2_1.Invoke(new EventHandler(delegate
                {
                    G2 = Convert.ToByte(tb_G2_1.Text);
                }));//参数
            }
            else
            {
                G2 = Convert.ToByte(tb_G2_1.Text);
            }

            if (this.tb_B2_1.InvokeRequired)
            {//使用委托来取值  怕出问题
                this.tb_B2_1.Invoke(new EventHandler(delegate
                {
                    B2 = Convert.ToByte(tb_B2_1.Text);
                }));//参数
            }
            else
            {
                B2 = Convert.ToByte(tb_B2_1.Text);
            }
            ////////////////////////////////////更新R3 G3 B3
            if (this.tb_R3_1.InvokeRequired)
            {//使用委托来取值  怕出问题
                this.tb_R3_1.Invoke(new EventHandler(delegate
                {
                    R3 = Convert.ToByte(tb_R3_1.Text);
                }));//参数
            }
            else
            {
                R3= Convert.ToByte(tb_R3_1.Text);
            }

            if (this.tb_G3_1.InvokeRequired)
            {//使用委托来取值  怕出问题
                this.tb_G3_1.Invoke(new EventHandler(delegate
                {
                    G3 = Convert.ToByte(tb_G3_1.Text);
                }));//参数
            }
            else
            {
                G3 = Convert.ToByte(tb_G3_1.Text);
            }

            if (this.tb_B3_1.InvokeRequired)
            {//使用委托来取值  怕出问题
                this.tb_B3_1.Invoke(new EventHandler(delegate
                {
                    B3 = Convert.ToByte(tb_B3_1.Text);
                }));//参数
            }
            else
            {
                B3 = Convert.ToByte(tb_B3_1.Text);
            }
            ////////////////////////////////////更新LEDmode speed density light LEDnumber
            if (this.tb_mod_1.InvokeRequired)
            {//使用委托来取值  怕出问题
                this.tb_mod_1.Invoke(new EventHandler(delegate
                {
                    LEDmode= Convert.ToByte(tb_mod_1.Text);
                }));//参数
            }
            else
            {
                LEDmode = Convert.ToByte(tb_mod_1.Text);
            }
            if (this.tb_Speed1.InvokeRequired)
            {//使用委托来取值  怕出问题
                this.tb_Speed1.Invoke(new EventHandler(delegate
                {
                    speed = Convert.ToByte(tb_Speed1.Text);
                }));//参数
            }
            else
            {
                speed = Convert.ToByte(tb_Speed1.Text);
            }
            if (this.tb_Density1.InvokeRequired)
            {//使用委托来取值  怕出问题
                this.tb_Density1.Invoke(new EventHandler(delegate
                {
                    density = Convert.ToByte(tb_Density1.Text);
                }));//参数
            }
            else
            {
                density = Convert.ToByte(tb_Density1.Text);
            }
            if (this.tb_Other1.InvokeRequired)
            {//使用委托来取值  怕出问题
                this.tb_Other1.Invoke(new EventHandler(delegate
                {
                    light = Convert.ToByte(tb_Other1.Text);
                }));//参数
            }
            else
            {
                light = Convert.ToByte(tb_Other1.Text);
            }
            if (this.tb_Num1.InvokeRequired)
            {//使用委托来取值  怕出问题
                this.tb_Num1.Invoke(new EventHandler(delegate
                {
                    led_number= Convert.ToByte(tb_Num1.Text);
                }));//参数
            }
            else
            {
                led_number = Convert.ToByte(tb_Num1.Text);
            }
        }
        private void vScrollBar_ValueChanged(object sender, EventArgs e)//垂直进度条有动作时   保证中间绿色点不变
        {//垂直进度条有动作时
            ///////////1、计算放大倍数   当前垂直滚动条值：5代表最大全屏图（放大倍数1），0代表最小（目前仅支持放大那么大，放大倍数32）
                float temp_num;float hScrollBar_Value_float = 0.0f;
                uint xxx=1;
                current_beishu_num = (xxx << (beishu - vScrollBar.Value));
                vScrollBar_Value = vScrollBar.Value;
            // 2、算出此时水平滚动条范围  依据此时lable_time，重新计算hScrollBar.Value值  totalTime / current_beishu_num为每一屏能显示的时间。
            // 算出此时pictrueBox 应该出现的波形图的范围      //总的样本数 num   要显示的样本 num_start 到 num_end   
           // hScrollBar_Maximum = (int)current_beishu_num - 1;//重设水平滚动条最大值，由于最大值变了hScrollBar也跟着变。
            hScrollBar_Maximum = (int)current_beishu_num *2 - 2;//新增的考虑（ 放大2倍时 0 1 2 ）（放大4倍时  0 1 2 3 4 5 6）（放大8倍时 0 1 2 。。。11 12 13 14）
            hScrollBar_Value_float = (float)(label_time / (totalTime / current_beishu_num));
            hScrollBar_Value = (int)hScrollBar_Value_float+ (int)hScrollBar_Value_float;//算出来是0   2   4  6  8  等
            temp_num = (float)num / (float)current_beishu_num;//算出每个放大了屏 要显示多少个样本
            if ((int)(hScrollBar_Value_float*2)- hScrollBar_Value==1)
            {//奇数的情况
                num_start = (uint)((uint)(hScrollBar_Value / 2) * temp_num) + (uint)(temp_num / 2);
                num_end = num_start + (uint)temp_num;
                hScrollBar_Value++;
            }
            else//偶数的情况
            {
                num_start = (uint)((hScrollBar_Value / 2) * temp_num);
                num_end = num_start + (uint)temp_num;
            }
            if (num_end > num) num_end = num;

            if (this.hScrollBar.InvokeRequired)
            {//使用委托来赋值
                this.hScrollBar.Invoke(new EventHandler(delegate
                {
                    hScrollBar.Maximum = hScrollBar_Maximum;
                    hScrollBar.Value = hScrollBar_Value;
                }));//参数
            }
            else
            {
                //重设水平滚动条最大值，由于最大值变了hScrollBar也跟着变。
                hScrollBar.Maximum = hScrollBar_Maximum;
                hScrollBar.Value = hScrollBar_Value;
            }

            cal_background();
            //重新计算当前绿线的位置 

            MouseClick_value = (int)(((label_time- BackGround_first_time)/ (BackGround_interval_time* WavScale))*WorkSpaceWidth);//MouseClick_value代表离pictruebox左上角原点 横坐标距离
            int zengliang = (int)((float)WorkSpaceWidth * (zengliang_time) / (BackGround_interval_time * WavScale));
            if (this.label_zc.InvokeRequired)//1、对 绿色标记条 位置进行重新赋值
            {//使用委托来赋值
                this.label_zc.Invoke(new EventHandler(delegate
                {
                    label_zc.Left = MouseClick_value + 10;//
                    label_zc.Show();
                }));//参数
            }
            else
            {
                label_zc.Left = MouseClick_value + 10;//
                label_zc.Show();
            }
            /////////////////////////2、对 绿色标记条 左边小条 位置进行重新赋值
            if (this.label_zc_left.InvokeRequired)//对 绿色标记条 左边小条 位置进行重新赋值
            {//使用委托来赋值
                this.label_zc_left.Invoke(new EventHandler(delegate
                {
                    label_zc_left.Left = MouseClick_value + 10 - zengliang;//
                    label_zc_left.Show();
                }));//参数
            }
            else
            {
                label_zc_left.Left = MouseClick_value + 10 - zengliang;//
                label_zc_left.Show();
            }
            /////////////////////////////3、对 绿色标记条 右边小条 位置进行重新赋值
            if (this.label_zc_right.InvokeRequired)//对 绿色标记条 右边小条 位置进行重新赋值
            {//使用委托来赋值
                this.label_zc_right.Invoke(new EventHandler(delegate
                {
                    label_zc_right.Left = MouseClick_value + 10 + zengliang;//
                    label_zc_right.Show();
                }));//参数
            }
            else
            {
                label_zc_right.Left = MouseClick_value + 10 + zengliang;//
                label_zc_right.Show();
            }


            ////////////////////
            drawWav_zc();
            TimeScaleValue();//水平时间内容重写
        }
        public void cal_background()
        {
            if (vScrollBar_Value == (vScrollBar_Maximum))//图片范围最大  全频谱图时
            {
                BackGround_first_time = 0;//图片范围最大时，首标定时间为0
                BackGround_interval_time = (float)totalTime / WavScale;
                BackGround_end_time = (float)totalTime;
            }
            else
            {
                if (hScrollBar_Value % 2 == 0)//偶数
                {
                    BackGround_first_time = ((float)(hScrollBar_Value / 2) * (float)totalTime) / (current_beishu_num);
                }
                else
                {
                    BackGround_first_time = (((float)(hScrollBar_Value / 2) + 0.5f) * (float)totalTime) / (current_beishu_num);
                }
                BackGround_interval_time = (float)totalTime / (WavScale * current_beishu_num);
                BackGround_end_time = BackGround_first_time + BackGround_interval_time * WavScale;
            }
        }
        private void Form1_Paint(object sender, PaintEventArgs e)
            {
               // drawWav_zc();
            }
        }
    }
