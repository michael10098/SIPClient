using System;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using SIPSorcery.Media;
using SIPSorceryMedia.Windows;
using SIPSorceryMedia.Abstractions;
using System.IO;
using System.Text.Json;

namespace SIPClient;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private string username;
    private string password;
    private string domain;
    private SIPTransport? sipTransport;
    private SIPUDPChannel? sipChannel;
    private SIPRegistrationUserAgent? regUserAgent;
    private WindowsAudioEndPoint? winAudio;
    private VoIPMediaSession? voIPMediaSession;
    private SIPUserAgent? userAgent;

    public MainWindow()
    {
        InitializeComponent();

        InitializeSIPSession();
        DisplayBox.Text = "9512859072";
    }

    private void InitializeSIPSession()
    {
        var json = File.ReadAllText("appsettings.local.json");
        var config = JsonSerializer.Deserialize<Config>(json);

        username = config.Voip.Username;
        password = config.Voip.Password;
        domain = config.Voip.Domain;

        sipTransport = new SIPTransport();
        sipChannel = new SIPUDPChannel(new System.Net.IPEndPoint(IPAddress.Any, 0));
        sipTransport.AddSIPChannel(sipChannel);
        regUserAgent = new SIPRegistrationUserAgent(
            sipTransport,
            username,
            password,
            domain,
            expiry: 300
        );
        regUserAgent.Start();

        winAudio = new WindowsAudioEndPoint(new AudioEncoder());
        winAudio.RestrictFormats(x => x.Codec == AudioCodecsEnum.PCMU);

        voIPMediaSession = new VoIPMediaSession(winAudio.ToMediaEndPoints())
        {
            AcceptRtpFromAny = true
        };

        userAgent = new SIPUserAgent(sipTransport, null);
    }

    private async Task<bool> MakeCall(string phoneNumber)
    {
        // Build destination SIP URI for voip.ms
        string destination = $"sip:{phoneNumber}@{domain}";

        return await userAgent!.Call(
            destination,
            username,
            password,
            voIPMediaSession
        );
    }

    private void DialButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Content is string digit)
        {
            DisplayBox.Text += digit;
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        DisplayBox.Clear();
    }

    private async void CallButton_Click(object sender, RoutedEventArgs e)
    {
        string phoneNumber = DisplayBox.Text;
        if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            MessageBox.Show($"Calling: {phoneNumber}", "SIP Call", MessageBoxButton.OK, MessageBoxImage.Information);
            // TODO: Implement actual SIP call logic
            bool callResult = await MakeCall(phoneNumber);
        }
    }

    private void AnswerButton_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Answer the SIP call
    }
}
