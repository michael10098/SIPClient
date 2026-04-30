public class Config
{
    public VoipConfig Voip { get; set; } = new();
}

public class VoipConfig
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Domain { get; set; } = "";
    public string IncomingCallNumber { get; set; } = "";
    public string DefaultCallNumber { get; set; } = "";
    public string ApiUsername { get; set; } = "";
    public string ApiPassword { get; set; } = "";
}