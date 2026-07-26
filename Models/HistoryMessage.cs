namespace Memo.AI.Models;

public class HistoryMessage
{

    public string MessageId { get; set; } = string.Empty;
    public string step { get; set; }

    public string Mailboxes { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string InReplyTo { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;
}