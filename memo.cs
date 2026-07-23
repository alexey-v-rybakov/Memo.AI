using System.Text.Json.Serialization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MemoAI.Models;

public class Memo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("uid")]
    public string Uid { get; set; } = "";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
}

public class MemoList
{
    [JsonPropertyName("memos")]
    public List<Memo> Memos { get; set; } = [];
}


public class MemosClient
{
    private readonly HttpClient _http;

    public MemosClient(string host, string token)
    {
        _http = new HttpClient();

        _http.BaseAddress = new Uri(host);

        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<List<Memo>> GetAllMemos()
    {
        var response = await _http.GetAsync("/api/v1/memos?pageSize=1000");

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        var result = JsonSerializer.Deserialize<MemoList>(json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        return result?.Memos ?? [];
    }

    public async Task UpdateMemo(Memo memo)
    {
        var body = new
        {
            content = memo.Content
        };

        var json = JsonSerializer.Serialize(body);

        var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/v1/{memo.Name}")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var response = await _http.SendAsync(request);

        response.EnsureSuccessStatusCode();
    }
}
