using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TradeTestCs
{
    class RequestManger
    {
        private static RequestManger instance = null;
        private Thread thread;

        public bool isRunning;
        public Queue<Task> tasks = new Queue<Task>();

        public const int Delay = 20;

        //경과시간 측정
        //private long time0, time1;
        //private float dt;
        //private float delayTime = 20;
        //private float delayTimer = 0; 

        //Request Manager는 프로그램 사용중 1번만 생성되도록 
        //생성자를 private처리함
        //Get()메소드 호출시 생성되고 클래스주소를 instance에 보관

        private RequestManger()
        {
            thread = new Thread(Request);
            thread.Start();
            //time1 = time0 = DateTime.Now.Ticks;
        }

        private void Request()
        {
            isRunning = true;

            try
            {
                Console.WriteLine("Request Thread Id : {0}", Thread.CurrentThread.ManagedThreadId);
                
                while (isRunning)
                {
                    
                    if (tasks.Count > 0)
                    {
                        //큐에서 태스크를 꺼내서 현재 쓰레드와 동기화시켜서 실행(즉 직접실행)합니다
                        tasks.Dequeue().RunSynchronously();
                    }

                    Thread.Sleep(Delay);
                }
            }
            catch (Exception e)
            {
                isRunning = false;
                instance = null;
                Console.WriteLine("Stop Request Thread: " + e.Message);
            }
            
        }

       

        public void RegistTask(Task task)
        {
            tasks.Enqueue(task);
        }

        

        public void Stop()
        {
            thread.Interrupt();
        }

        public static RequestManger Get()
        {
            if(instance == null)
            {
                instance = new RequestManger();
            }
            return instance;
        }

    }
}
