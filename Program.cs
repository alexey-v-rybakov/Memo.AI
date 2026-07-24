using System;
using System.Threading.Tasks;
using MemoAI.Models;
//using Newtonsoft.Json;
//using Newtonsoft.Json.Linq;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using MimeKit;
using Serilog;
using Serilog.Settings.Configuration;



namespace MemosAI
{
    internal class Program
    {
        private static ManualResetEvent thdStopEvent = new ManualResetEvent(false);
        
        static async Task Main(string[] args)
        {
            // Настройка логирования
           var config = new ConfigurationBuilder()
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                    .AddJsonFile("logsettings.json", optional: false, reloadOnChange: true)
                    .Build();

            Log.Logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(config)
                    .CreateLogger();
            
            Log.Information("========== STCMedBoot запущен ============");

            // Читаем настройки

            AppConfig.ReadConfig();    

            // Create a new thread and start it
             System.Threading.Thread workerThread = new System.Threading.Thread(WorkerMethod);
            workerThread.Start();

            Console.WriteLine();
            Console.WriteLine("Нажмите любую клавишу...");
            Console.ReadKey();

            thdStopEvent.Set();

            workerThread.Join();
            Console.WriteLine("Поток завершен");
   
        }

        private static void WorkerMethod()
        {            
            while (!thdStopEvent.WaitOne(10000))
            {
                Console.WriteLine("Thread is running...");
                System.Threading.Thread.Sleep(1000); // Sleep for 1 second

            }

            Console.WriteLine("Thread has exited.");
        }
    }
}