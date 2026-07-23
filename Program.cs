using System;
using System.Threading.Tasks;
using MemoAI.Models;
using Microsoft.SemanticKernel;

namespace MemosAI
{
    internal class Program
    {
        // Лучше потом перенести в appsettings.json
        private const string Host = "https://memo.feshman-tech.ru";
        private const string Token = "memos_pat_IVxXijTXHTTdu5tZ66AHwWWewcm6Yhzl";

        static async Task Main(string[] args)
        {
            var builder = Kernel.CreateBuilder();

builder.AddOpenAIChatCompletion(
    modelId: "google/gemma-3-12b",
    apiKey: "lm-studio",
    endpoint: new Uri("http://192.168.1.10:1234/v1"));

var kernel = builder.Build();


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

                        await client.UpdateMemo(memo);

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
            Console.WriteLine("Обработка AI...");

            // Здесь позже будет вызов OpenAI / LM Studio / Ollama

            memo.Content +=
                $"\n\n---\nОбработано AI {DateTime.Now:G}";

            await Task.CompletedTask;
        }
    }
}