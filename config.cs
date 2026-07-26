using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
public class Config
{
    public LLM_Config? LLM { get; set; }
    public Mail_Config? Mail { get; set; }
    public Memo_Config? Memo { get; set; }
}

public class LLM_Config
{
    public string? ModelId { get; set; }
    public string? ApiKey { get; set; }
    public string? Endpoint { get; set; }
}

public class Mail_Config
{
    public string? ImapServer { get; set; }
    public int? ImapPort { get; set; }
    public string? SmtpServer { get; set; }
    public int? SmtpPort { get; set; }
    public string? Login { get; set; }
    public string? Password { get; set; }
}

public class Memo_Config
{
    public string? Host { get; set; }
    public string? Token { get; set; }
}


class AppConfig
{
    public static Config? m_config = new Config();
    public static void ReadConfig()
    {
        
        string jsonContent = File.ReadAllText("appsettings.json");
        m_config = JsonConvert.DeserializeObject<Config>(jsonContent);
    }

}