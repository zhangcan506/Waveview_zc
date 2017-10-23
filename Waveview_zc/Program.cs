using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.Collections;
using System.Threading;


namespace Waveview_zc
{
    static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form_zc());
           // Test aa = new Waveview_zc.Test();
           // aa.Run();
            
        }

    }
    public class Test
    {

        public void Run()
        {
            Test obj = new Test();
            Console.WriteLine(Thread.CurrentThread.ManagedThreadId.ToString());

       //方法一：调用线程执行方法，在方法中实现死循环，每个循环Sleep设定时间
       //     Thread thread = new Thread(new ThreadStart(obj.Method1));
       //     thread.Start();


            //方法二：使用System.Timers.Timer类
            System.Timers.Timer t = new System.Timers.Timer(100);//实例化Timer类，设置时间间隔
            t.Elapsed += new System.Timers.ElapsedEventHandler(obj.Method2);//到达时间的时候执行事件
            t.AutoReset = true;//设置是执行一次（false）还是一直执行(true)
            t.Enabled = true;//是否执行System.Timers.Timer.Elapsed事件
            while (true)
            {
                Console.WriteLine("test_" + Thread.CurrentThread.ManagedThreadId.ToString());
                Thread.Sleep(100);
            }


            //方法三：使用System.Threading.Timer
            //Timer构造函数参数说明：
            //Callback：一个 TimerCallback 委托，表示要执行的方法。
            //State：一个包含回调方法要使用的信息的对象，或者为空引用（Visual Basic 中为 Nothing）。
            //dueTime：调用 callback 之前延迟的时间量（以毫秒为单位）。指定 Timeout.Infinite 以防止计时器开始计时。指定零 (0) 以立即启动计时器。
            //Period：调用 callback 的时间间隔（以毫秒为单位）。指定 Timeout.Infinite 可以禁用定期终止。
  //          System.Threading.Timer threadTimer = new System.Threading.Timer(new System.Threading.TimerCallback(obj.Method3), null, 0, 100);
 //           while (true)
  //          {
  //              Console.WriteLine("test_" + Thread.CurrentThread.ManagedThreadId.ToString());
  //              Thread.Sleep(100);
 //           }

 //           Console.ReadLine();
        }


        void Method1()
        {
            while (true)
            {
                Console.WriteLine(DateTime.Now.ToString() + "_" + Thread.CurrentThread.ManagedThreadId.ToString());
                Thread.CurrentThread.Join(100);//阻止设定时间
            }
        }


        void Method2(object source, System.Timers.ElapsedEventArgs e)
        {
            //Console.WriteLine(DateTime.Now.ToString() + "_" + Thread.CurrentThread.ManagedThreadId.ToString());
            Console.WriteLine(DateTime.Now.ToString() + "_" + DateTime.Now.Millisecond.ToString());
        }


        void Method3(Object state)
        {
            Console.WriteLine(DateTime.Now.ToString() + "_" + Thread.CurrentThread.ManagedThreadId.ToString());
        }
    }
}
