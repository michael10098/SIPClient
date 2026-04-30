using System.Net.Http;
using System.Text.Json;

/// <summary>
/// SMS Functions
/// </summary>
class SMS
{
    /// <summary>
    /// Send an SMS message.
    /// </summary>
    /// <param name="apiUsername">The email of the voip.me user.</param>
    /// <param name="apiPassword">The password that was set in voip.me for SMS.</param>
    /// <param name="did">The voip.ms DID number.</param>
    /// <param name="destination">The destination phone number.</param>
    /// <param name="message">The message to send</param>
    /// <returns></returns>
    static public async Task<string> SendSMSViaVoipMs(
        string apiUsername,   // your voip.ms account email
        string apiPassword,   // your API password (set in voip.ms portal)
        string did,           // your voip.ms DID number, e.g. 5551234567
        string destination,   // recipient number, e.g. 5559876543
        string message)
    {
        // build a REST request to voip.me.
        var url = "https://voip.ms/api/v1/rest.php" +
                $"?api_username={Uri.EscapeDataString(apiUsername)}" +
                $"&api_password={Uri.EscapeDataString(apiPassword)}" +
                $"&method=sendSMS" +
                $"&did={did}" +
                $"&dst={destination}" +
                $"&message={Uri.EscapeDataString(message)}";

        // setup a http client
        using var http = new HttpClient();
        // send the REST request
        var response = await http.GetStringAsync(url);
        return response; // Returns JSON with status        
    }

    /// <summary>
    /// Get new messages.  The needs to be polled.
    /// </summary>
    /// <param name="apiUsername">The email of the voip.me user.</param>
    /// <param name="apiPassword">The password that was set in voip.me for SMS.</param>
    /// <param name="did">The voip.ms DID number.</param>
    /// <returns>Return a list of SMSMessages.</returns>
    static public async Task<List<SmsMessage>> GetNewMessages(
        string apiUsername,
        string apiPassword,
        string did)
    {
        // Use local time and fetch a 2-day window to avoid timezone edge cases
        var from = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");
        var to = DateTime.Now.ToString("yyyy-MM-dd");

        // setup a REST call to voip.me
        var url = "https://voip.ms/api/v1/rest.php" +
                $"?api_username={Uri.EscapeDataString(apiUsername)}" +
                $"&api_password={Uri.EscapeDataString(apiPassword)}" +
                $"&method=getSMS" +
                $"&did={did}" +
                $"&from={from}" +
                $"&to={to}" +
                $"&type=1"; // 1 = inbound messages

        // create a new http client
        using var http = new HttpClient();
        // send the REST call
        var response = await http.GetStringAsync(url);
        // parse the json inside the response
        var json = JsonDocument.Parse(response);

        // if we didn't get a good response, then return an empty list
        if (json.RootElement.GetProperty("status").GetString() != "success")
            return new List<SmsMessage>();

        // return the list of SMSMessages
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