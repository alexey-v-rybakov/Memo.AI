namespace Memo.AI.Models;

public class HistoryChain
{
    public string Id { get; set; }
    public string OriginalMessage { get; set; }
    public string ClarificationQuestions { get; set; }
    public List<HistoryMessage> Messages { get; set; } = new();
}