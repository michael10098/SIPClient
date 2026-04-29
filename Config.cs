public class Config
{
    public VoipConfig Voip { get; set; } = new();
}

public class VoipConfig
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Domain { get; set; } = "";
}