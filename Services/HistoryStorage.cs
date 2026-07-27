using System.Security.Claims;
using System.Text.Json;
using Memo.AI.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Serilog;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using MimeKit;
using System.Text.Encodings.Web;

namespace Memo.AI.Services;

public class HistoryStorage
{
    private readonly string _rootPath;

    private readonly JsonSerializerOptions _jsonOptions =
        new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };


    public HistoryStorage(string rootPath)
    {
        _rootPath = rootPath;
    }
    

    public List<HistoryChain> m_historyChain;

    /// <summary>
    /// Загрузить все истории из всех GUID каталогов
    /// </summary>
    public void LoadAll()
    {
        m_historyChain = new List<HistoryChain>();

        if (!Directory.Exists(_rootPath))
            return;


        foreach (var directory in Directory.GetDirectories(_rootPath))
        {
            var folderName = Path.GetFileName(directory);

            string id = folderName;
            //if (!Guid.TryParse(folderName, out var id))
            //    continue;
            var file = Path.Combine(directory, "history.json");
            if (!File.Exists(file))
                continue;

            var json = File.ReadAllText(file);
            var messages = JsonSerializer.Deserialize<List<HistoryMessage>>
                    (
                        json,
                        _jsonOptions
                    )
                    ?? new List<HistoryMessage>();

            m_historyChain.Add(
                new HistoryChain
                    {
                        Id = id,
                        Messages = messages
                    });
        }
        return;
    }

    //
    // Получить базовый идентификатор переписки
    //
    public string GetBaseID(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return "";    

        foreach (var chain in m_historyChain)
        {
            foreach (var message in chain.Messages)
                {
                    if (message.MessageId == reference)
                    {
                        return chain.Id;
                    }
                }
        }
        return "";   
    }

    /// <summary>
    /// Загрузить одну историю по GUID
    /// </summary>
    public async Task<HistoryChain?> LoadAsync(string id)
    {
        var file =
            Path.Combine(
                _rootPath,
                id.ToString(),
                "history.json");


        if (!File.Exists(file))
            return null;


        var json = await File.ReadAllTextAsync(file);


        var messages =
            JsonSerializer.Deserialize<List<HistoryMessage>>
            (
                json,
                _jsonOptions
            )
            ?? new();


        return new HistoryChain
        {
            Id = id,
            Messages = messages
        };
    }



    /// <summary>
    /// Сохранить/обновить историю по GUID
    /// </summary>
    public async Task SaveAsync(
        string id,
        List<HistoryMessage> messages)
    {

        var directory =
            Path.Combine(
                _rootPath,
                id.ToString());


        Directory.CreateDirectory(directory);


        var file =
            Path.Combine(
                directory,
                "history.json");


        var json =
            JsonSerializer.Serialize(
                messages,
                _jsonOptions);


        await File.WriteAllTextAsync(
            file,
            json);
    }



   /// <summary>
    /// Добавить сообщение в существующую историю
    /// </summary>
    public void AddChain(string base_id)
    {
        //
        HistoryChain chain = m_historyChain.FirstOrDefault(c => c.Id == base_id);

        if (chain == null)
        {
            chain       = new HistoryChain();
            chain.Id    = base_id;
            m_historyChain.Add(chain);
        }
        SaveHistoryChain(base_id);
    }

    /// <summary>
    /// Добавить сообщение в существующую историю
    /// </summary>
    public void ProcessMessage(string base_id, HistoryMessage message)
    {
        //
        HistoryChain chain = m_historyChain.FirstOrDefault(c => c.Id == base_id);

        if (chain == null)
        {
            chain       = new HistoryChain();
            chain.Id    = base_id;
            m_historyChain.Add(chain);
        }
        // 
        // Первое сообщение от пользователя
        if (string.IsNullOrWhiteSpace(message.InReplyTo))
        {
            // Удалить всю историю
            chain.Messages.Clear();
            message.step = "start";
            chain.Messages.Add(message);
            //
            HistoryMessage clr_msg = RequestUserClarificationQuestion(message);
            SendMailToUser(clr_msg);
            chain.Messages.Add(clr_msg);

        }
        else // пользователь ответил на какое-то сообщение
        {
            //
            // Найти сообщение на которое ответили
            foreach (HistoryMessage hm in chain.Messages)
            {
                if (hm.MessageId == message.InReplyTo)
                {
                    Log.Information(@"Найдено исходное сообщение: {hm.step}");

                    if (hm.step == "clarification")
                    {  
                        message.step = "clarification_answer";
                        chain.Messages.Add(message); // добавить текущее сообщение
                        HistoryMessage clr_msg = RequestUserArchitect(base_id, message);  
                        SendMailToUser(clr_msg);
                        chain.Messages.Add(clr_msg);
                        break;
                    }
                    else
                    if (hm.step == "architect")
                    {
                        message.step = "architect_answer";
                        chain.Messages.Add(message); // добавить текущее сообщение
                        HistoryMessage clr_msg = RequestUserMakeDraft(base_id, message);  
                        SendMailToUser(clr_msg);
                        chain.Messages.Add(clr_msg);
                        break;
                        
                    }
                    else
                    if (hm.step == "draft")
                    {
                        message.step = "draft_answer";
                        chain.Messages.Add(message); // добавить текущее сообщение
                        HistoryMessage clr_msg = RequestUserRewrite(base_id, message);  
                        SendMailToUser(clr_msg);
                        chain.Messages.Add(clr_msg);
                        break;
                      
                    }
                }

            }
        }

        SaveHistoryChain(base_id);
    }

    public HistoryMessage RequestUserClarificationQuestion(HistoryMessage o_msg)
    {
        var llm_builder = Kernel.CreateBuilder();
        llm_builder.AddOpenAIChatCompletion(
                                        modelId: AppConfig.m_config.LLM.ModelId,
                                        endpoint: new Uri(AppConfig.m_config.LLM.Endpoint),
                                        apiKey: AppConfig.m_config.LLM.ApiKey);

        var llm_kernel = llm_builder.Build();
        var chat = llm_kernel.GetRequiredService<IChatCompletionService>();
        string ai_req = System.IO.File.ReadAllText(@"PROMPTS\ClarificationQuestion.txt") + o_msg.Body;
        Log.Information("Запрос AI...");
        var response = chat.GetChatMessageContentAsync(ai_req).GetAwaiter().GetResult();
        Log.Information(response.Content);


        HistoryMessage clr_msg = new HistoryMessage();
        clr_msg.step        = "clarification";
        clr_msg.Subject     = o_msg.Subject;
        clr_msg.InReplyTo   = o_msg.MessageId;
        clr_msg.Mailboxes   = o_msg.Mailboxes;
        clr_msg.Body        = response.Content;
        

        return clr_msg;
    } 

       public HistoryMessage RequestUserArchitect(string base_id, HistoryMessage o_msg)
    {
        var llm_builder = Kernel.CreateBuilder();
        llm_builder.AddOpenAIChatCompletion(
                                        modelId: AppConfig.m_config.LLM.ModelId,
                                        endpoint: new Uri(AppConfig.m_config.LLM.Endpoint),
                                        apiKey: AppConfig.m_config.LLM.ApiKey);

        var llm_kernel = llm_builder.Build();
        var chat = llm_kernel.GetRequiredService<IChatCompletionService>();
        string ai_req = System.IO.File.ReadAllText(@"PROMPTS\architect.txt");
        Log.Information("Запрос AI...");

        HistoryChain chain = m_historyChain.FirstOrDefault(c => c.Id == base_id);

        //
            // Найти сообщение на которое ответили
        foreach (HistoryMessage hm in chain.Messages)
        {
            if (hm.step == "start")
            {
                ai_req = ai_req.Replace("{start}", hm.Body);
            }
            else
            if (hm.step == "clarification")
            {
                  ai_req =  ai_req.Replace("{clarification}", hm.Body);
            }
            else
            if (hm.step == "clarification_answer")
            {
                ai_req = ai_req.Replace("{clarification_answer}", hm.Body);
            }

        }


        // Найти start

        // найти clarification

        // найти clarification_answer




        var response = chat.GetChatMessageContentAsync(ai_req).GetAwaiter().GetResult();
        Log.Information(response.Content);

        string m = "Я подготовил для тебя структуру будущей записи. Прочитай, исправь акценты если ты что хочешь поменять. Ответь на дополнительные вопросы, если они заданы. " + response.Content;

        HistoryMessage clr_msg = new HistoryMessage();
        clr_msg.step        = "architect";
        clr_msg.Subject     = o_msg.Subject;
        clr_msg.InReplyTo   = o_msg.MessageId;
        clr_msg.Mailboxes   = o_msg.Mailboxes;
        clr_msg.Body        = m; //response.Content;
        

        return clr_msg;
    } 

  public HistoryMessage RequestUserMakeDraft(string base_id, HistoryMessage o_msg)
    {
        var llm_builder = Kernel.CreateBuilder();
        llm_builder.AddOpenAIChatCompletion(
                                        modelId: AppConfig.m_config.LLM.ModelId,
                                        endpoint: new Uri(AppConfig.m_config.LLM.Endpoint),
                                        apiKey: AppConfig.m_config.LLM.ApiKey);

        var llm_kernel = llm_builder.Build();
        var chat = llm_kernel.GetRequiredService<IChatCompletionService>();
        string ai_req = System.IO.File.ReadAllText(@"PROMPTS\makeDraft.txt");
        Log.Information("Запрос AI...");

        HistoryChain chain = m_historyChain.FirstOrDefault(c => c.Id == base_id);

        //
            // Найти сообщение на которое ответили
        foreach (HistoryMessage hm in chain.Messages)
        {
            if (hm.step == "start")
            {
                ai_req = ai_req.Replace("{start}", hm.Body);
            }
            else
            if (hm.step == "clarification")
            {
                  ai_req =  ai_req.Replace("{clarification}", hm.Body);
            }
            else
            if (hm.step == "clarification_answer")
            {
                ai_req = ai_req.Replace("{clarification_answer}", hm.Body);
            }
            else
            if (hm.step == "architect")
            {
                ai_req = ai_req.Replace("{architect}", hm.Body);
            }
            else
            if (hm.step == "architect_answer")
            {
                ai_req = ai_req.Replace("{architect_answer}", hm.Body);
            }


        }


        // Найти start

        // найти clarification

        // найти clarification_answer




        var response = chat.GetChatMessageContentAsync(ai_req).GetAwaiter().GetResult();
        Log.Information(response.Content);

        string m = "Я подготовил для тебя черновик. Что-то нужно исправить ли можно публиковать? " + response.Content;

        HistoryMessage clr_msg = new HistoryMessage();
        clr_msg.step        = "draft";
        clr_msg.Subject     = o_msg.Subject;
        clr_msg.InReplyTo   = o_msg.MessageId;
        clr_msg.Mailboxes   = o_msg.Mailboxes;
        clr_msg.Body        = m; //response.Content;
        

        return clr_msg;
    }

      public HistoryMessage RequestUserRewrite(string base_id, HistoryMessage o_msg)
    {
        var llm_builder = Kernel.CreateBuilder();
        llm_builder.AddOpenAIChatCompletion(
                                        modelId: AppConfig.m_config.LLM.ModelId,
                                        endpoint: new Uri(AppConfig.m_config.LLM.Endpoint),
                                        apiKey: AppConfig.m_config.LLM.ApiKey);

        var llm_kernel = llm_builder.Build();
        var chat = llm_kernel.GetRequiredService<IChatCompletionService>();
        string ai_req = System.IO.File.ReadAllText(@"PROMPTS\allow_public.txt");
        Log.Information("Запрос AI...");


        HistoryChain chain = m_historyChain.FirstOrDefault(c => c.Id == base_id);

        //
            // Найти сообщение на которое ответили
        foreach (HistoryMessage hm in chain.Messages)
        {
            if (hm.step == "start")
            {
                ai_req = ai_req.Replace("{start}", hm.Body);
            }
            else
            if (hm.step == "clarification")
            {
                  ai_req =  ai_req.Replace("{clarification}", hm.Body);
            }
            else
            if (hm.step == "clarification_answer")
            {
                ai_req = ai_req.Replace("{clarification_answer}", hm.Body);
            }
            else
            if (hm.step == "architect")
            {
                ai_req = ai_req.Replace("{architect}", hm.Body);
            }
            else
            if (hm.step == "architect_answer")
            {
                ai_req = ai_req.Replace("{architect_answer}", hm.Body);
            }
            else
            if (hm.step == "draft")
            {
                ai_req = ai_req.Replace("{draft}", hm.Body);
            }
            else
            if (hm.step == "draft_answer")
            {
                ai_req = ai_req.Replace("{draft_answer}", hm.Body);
            }



        }




        var response = chat.GetChatMessageContentAsync(ai_req).GetAwaiter().GetResult();
        Log.Information(response.Content);

        HistoryMessage clr_msg = new HistoryMessage();
        clr_msg.step        = "draft";
        clr_msg.Subject     = o_msg.Subject;
        clr_msg.InReplyTo   = o_msg.MessageId;
        clr_msg.Mailboxes   = o_msg.Mailboxes;
        clr_msg.Body        = response.Content;
        

        return clr_msg;
    }

        //
        // Отправка ответа
        public static void SendMailToUser(HistoryMessage replay_info)
        {

            var reply = new MimeMessage();

            reply.From.Add(MailboxAddress.Parse(AppConfig.m_config.Mail.Login));
            reply.To.Add(MailboxAddress.Parse(replay_info.Mailboxes));
            reply.Subject = replay_info.Subject;
            reply.InReplyTo = replay_info.InReplyTo;

            reply.Body = new TextPart("plain")
                {
                    Text = replay_info.Body
                    
                };

            using var smtp = new SmtpClient();
            smtp.ServerCertificateValidationCallback =  (sender, certificate, chain, errors) => true;
            smtp.Connect(AppConfig.m_config.Mail.SmtpServer, (int)AppConfig.m_config.Mail.SmtpPort,  MailKit.Security.SecureSocketOptions.SslOnConnect);

            smtp.Authenticate(AppConfig.m_config.Mail.Login, AppConfig.m_config.Mail.Password);

            smtp.Send(reply);
            replay_info.MessageId = reply.MessageId;
            smtp.Disconnect(true);

                        
            Log.Information("Ответ отправлен");
        }


    public bool SaveHistoryChain(string id)
    {
        var chain = m_historyChain.FirstOrDefault(c => c.Id == id);

        if (chain == null)
            return false;

        var directory = Path.Combine(_rootPath, id.ToString());

        // Создать каталог, если его нет
        Directory.CreateDirectory(directory);

        var fileName = Path.Combine(directory, "history.json");

        var options = new JsonSerializerOptions
        {

            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

        var json = JsonSerializer.Serialize(chain.Messages, options);

        File.WriteAllText(fileName, json);

        return true;
    }
    
}