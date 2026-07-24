using System;
using System.Threading.Tasks;
using MemoAI.Models;
//using Newtonsoft.Json;
//using Newtonsoft.Json.Linq;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace MemosAI
{
    internal class Program
    {
        // Лучше потом перенести в appsettings.json
        private const string Host = "https://memo.feshman-tech.ru";
        private const string Token = "memos_pat_IVxXijTXHTTdu5tZ66AHwWWewcm6Yhzl";
        // Класс для работы с моделью
        private static string modelId = "";
        private static string apiKey = "";
        private static string endpoint = "";
       
        static async Task Main(string[] args)
        {
            // Чтение конфигурации из файла
            var config = new ConfigurationBuilder()
                                .AddJsonFile("appsettings.json", optional: false)
                                .Build();

            modelId = config["LLM:ModelId"]!;
            apiKey = config["LLM:ApiKey"]!;
            endpoint = config["LLM:Endpoint"]!;

            Console.WriteLine("=== Memos AI Agent ===");
            Console.WriteLine();

            try
            {
                var client = new MemosClient(Host, Token);

                Console.WriteLine("Получение заметок...");

                var memos = await client.GetAllMemos();

                Console.WriteLine($"Получено {memos.Count} заметок.");
                Console.WriteLine();

                int processed = 0;

                foreach (var memo in memos)
                {
                    Console.WriteLine("--------------------------------------");
                    Console.WriteLine($"UID: {memo.Uid}");
                    Console.WriteLine($"Name: {memo.Name}");

                    try
                    {
                       
                        await ProcessMemo(memo);

                        //await client.UpdateMemo(memo);

                        processed++;

                        Console.WriteLine("✓ Заметка обновлена.");
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Ошибка обработки: {ex.Message}");
                        Console.ResetColor();
                    }

                    Console.WriteLine();
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("--------------------------------------");
                Console.WriteLine($"Готово. Обработано {processed} из {memos.Count} заметок.");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex);
                Console.ResetColor();
            }

            Console.WriteLine();
            Console.WriteLine("Нажмите любую клавишу...");
            Console.ReadKey();
        }

        private static async Task ProcessMemo(Memo memo)
        {
            var llm_builder = Kernel.CreateBuilder();
            llm_builder.AddOpenAIChatCompletion(
                                                    modelId: modelId!,
                                                    apiKey: apiKey!,
                                                    endpoint: new Uri(endpoint!)
                                                );

            var llm_kernel = llm_builder.Build();
            var chat = llm_kernel.GetRequiredService<IChatCompletionService>();

            Console.WriteLine("Обработка AI...");

            // Здесь позже будет вызов OpenAI / LM Studio / Ollama
            if (memo.Content.Contains("#aip"))
            {
                var response = await chat.GetChatMessageContentAsync(
    "Прочитай мою заметки и предложи 3-4 тега для нее. Ответ должен быть в json содержащий только теги. Отсоритруй теги в порядке релевантности  " + memo.Content);
                    Console.WriteLine(response.Content);

                // Здесь можно добавить дополнительную логику для обработки тега
                Console.WriteLine("Тег #aip найден в заметке.");
            }




            memo.Content += $"\n\n---\nОбработано AI {DateTime.Now:G}";

            await Task.CompletedTask;
        }
    }
}