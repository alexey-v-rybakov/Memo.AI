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
using Memo.AI.Services;
using Memo.AI.Models;



namespace MemosAI
{
    internal class Program
    {
        private static ManualResetEvent thdStopEvent = new ManualResetEvent(false);
        //
        // Хранение цепочки цифр
        private static HistoryStorage m_historyStorage = new HistoryStorage(@"HISTORY\");
        
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
            
            Log.Information("========== Memo.AI agents running ============");

            // Читаем настройки
            AppConfig.ReadConfig();
            // Читаем цепочки писем
            m_historyStorage.LoadAll();


            // Create a new thread and start it
             System.Threading.Thread workerThread = new System.Threading.Thread(WorkerMethod);
            workerThread.Start();

            Console.WriteLine();
            Console.WriteLine("Нажмите любую клавишу...");
            //Console.ReadKey();
            Console.ReadLine();
            thdStopEvent.Set();

            workerThread.Join();
            Console.WriteLine("Поток завершен");
   
        }

        private static void WorkerMethod()
        {    

            Log.Information("--- Рабочий поток запущен --- ");

            using var client = new ImapClient();
            client.Connect(AppConfig.m_config.Mail.ImapServer, (int)AppConfig.m_config.Mail.ImapPort, true);
            client.Authenticate(AppConfig.m_config.Mail.Login, AppConfig.m_config.Mail.Password);

            var inbox = client.Inbox;

            inbox.Open(MailKit.FolderAccess.ReadWrite);

            Log.Information($"Писем в ящике: {inbox.Count}");

            while (!thdStopEvent.WaitOne(1000))
            {
                // Поиск новых писем
                var uids = inbox.Search(SearchQuery.NotSeen);
                //
                // 
                foreach(var uid in uids)
                {
                    var message = inbox.GetMessage(uid);

                    Log.Information("----------------");

                    Log.Information("MessageId: {MessageId}", message.MessageId);
                    Log.Information("Subject: {Subject}", message.Subject);

                    Log.Information("From: {From}", message.From);
                    Log.Information("Sender: {Sender}", message.Sender);
                    Log.Information("ReplyTo: {ReplyTo}", message.ReplyTo);

                    Log.Information("To: {To}", message.To);
                    Log.Information("Cc: {Cc}", message.Cc);
                    Log.Information("Bcc: {Bcc}", message.Bcc);

                    Log.Information("Date: {Date}", message.Date);

                    Log.Information("InReplyTo: {InReplyTo}", message.InReplyTo);
                    Log.Information("Importance: {Importance}", message.Importance);
                    Log.Information("Priority: {Priority}", message.Priority);
                    Log.Information("XPriority: {XPriority}", message.XPriority);

                    Log.Information("TextBody:");
                    Log.Information(message.TextBody);
                    Log.Information("----------------");

                    // базовый идентификатор цепочки писем
                    string baseID = "";
                    Log.Information("References:");
                    foreach (var reference in message.References)
                    {
                        Log.Information("  {Reference}", reference);
                        //
                        // Получить базовый идентификатор цепочки писем
                        if (!string.IsNullOrWhiteSpace(reference))
                        {
                            baseID = m_historyStorage.GetBaseID(reference);
                            if (!string.IsNullOrWhiteSpace(baseID))
                                break;
                        }
                    }
                    
                    HistoryMessage hm = new HistoryMessage();
                    hm.MessageId    = message.MessageId;
                    hm.Mailboxes    = message.From.ToString();
                    hm.Subject      = message.Subject;
                    hm.Body         = message.TextBody;
                    hm.InReplyTo    = message.InReplyTo;         
                    //
                    //
                    // Если идентификатор не нашли, формируем новую запись и сохраняем на диск
                    if (string.IsNullOrWhiteSpace(baseID))
                    {
                        baseID      = hm.MessageId;
                        //m_historyStorage.AddChain(baseID);
                    }
                    m_historyStorage.ProcessMessage(baseID, hm);
                    //
                    //Reply(message);
                    //
                    inbox.AddFlags(uid, MessageFlags.Seen,  true);
                }
            }

            client.Disconnect(true);
            
            Log.Information("--- Рабочий поток остановлен --- ");

        }


        //
        // Отправка ответа
        public static void Reply(MimeMessage original)
        {

            var reply = new MimeMessage();

            reply.From.Add(MailboxAddress.Parse(AppConfig.m_config.Mail.Login));
            reply.To.Add(original.From.Mailboxes.First());
            reply.Subject = original.Subject;
            reply.InReplyTo = original.MessageId;

            if (!string.IsNullOrEmpty(original.MessageId))
            {
                reply.InReplyTo = original.MessageId;
                reply.References.Add(original.MessageId);
            }

            // Получаем GUID цепочки или создаем новый
            var conversationId = original.Headers["X-Memo-Conversation"];

            if (string.IsNullOrWhiteSpace(conversationId))
                conversationId = Guid.NewGuid().ToString();

            // Добавляем собственный заголовок
            reply.Headers.Add("X-Memo-Conversation", conversationId);

            reply.Body = new TextPart("plain")
                {
                    Text =
                    """
                    Принято.

                    Ваша мысль получена и будет обработана AI-агентами.
                    """
                };

            using var smtp = new SmtpClient();
            smtp.ServerCertificateValidationCallback =  (sender, certificate, chain, errors) => true;
            smtp.Connect(AppConfig.m_config.Mail.SmtpServer, (int)AppConfig.m_config.Mail.SmtpPort,  MailKit.Security.SecureSocketOptions.SslOnConnect);

            smtp.Authenticate(AppConfig.m_config.Mail.Login, AppConfig.m_config.Mail.Password);

            smtp.Send(reply);
            smtp.Disconnect(true);


            Log.Information("Ответ отправлен");
        }
    }
}