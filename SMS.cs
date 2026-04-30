using System.Net.Http;
using System.Text.Json;

class SMS
{
    static public async Task<string> SendSMSViaVoipMs(
        string apiUsername,   // your voip.ms account email
        string apiPassword,   // your API password (set in voip.ms portal)
        string did,           // your voip.ms DID number, e.g. 5551234567
        string destination,   // recipient number, e.g. 5559876543
        string message)
    {
        var url = "https://voip.ms/api/v1/rest.php" +
                $"?api_username={Uri.EscapeDataString(apiUsername)}" +
                $"&api_password={Uri.EscapeDataString(apiPassword)}" +
                $"&method=sendSMS" +
                $"&did={did}" +
                $"&dst={destination}" +
                $"&message={Uri.EscapeDataString(message)}";

        using var http = new HttpClient();
        var response = await http.GetStringAsync(url);
        return response; // Returns JSON with status        
    }

    static public async Task<List<SmsMessage>> GetNewMessages(
        string apiUsername,
        string apiPassword,
        string did)
    {
        // Use local time and fetch a 2-day window to avoid timezone edge cases
        var from = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");
        var to = DateTime.Now.ToString("yyyy-MM-dd");

        var url = "https://voip.ms/api/v1/rest.php" +
                $"?api_username={Uri.EscapeDataString(apiUsername)}" +
                $"&api_password={Uri.EscapeDataString(apiPassword)}" +
                $"&method=getSMS" +
                $"&did={did}" +
                $"&from={from}" +
                $"&to={to}" +
                $"&type=1"; // 1 = inbound messages

        using var http = new HttpClient();
        var response = await http.GetStringAsync(url);
        var json = JsonDocument.Parse(response);

        if (json.RootElement.GetProperty("status").GetString() != "success")
            return new List<SmsMessage>();

        return json.RootElement
            .GetProperty("sms")
            .EnumerateArray()
            .Select(s => new SmsMessage
            {
                Id = s.GetProperty("id").GetString() ?? "",
                From = s.GetProperty("contact").GetString() ?? "",
                Message = s.GetProperty("message").GetString() ?? "",
                Date = s.GetProperty("date").GetString() ?? ""
            })
            .ToList();
    }

    public class SmsMessage
    {
        public string Id { get; set; } = "";
        public string From { get; set; } = "";
        public string Message { get; set; } = "";
        public string Date { get; set; } = "";
    }    
}