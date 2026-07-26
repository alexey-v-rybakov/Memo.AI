using System.Security.Claims;
using System.Text.Json;
using Memo.AI.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Serilog;

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
            SendMessage(clr_msg);
            chain.Messages.Add(clr_msg);

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


        return new HistoryMessage();
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
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(chain.Messages, options);

        File.WriteAllText(fileName, json);

        return true;
    }
    
}