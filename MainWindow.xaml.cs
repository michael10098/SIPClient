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
using System.Media;
using System.Threading.Tasks;

namespace SIPClient;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private string username = "";
    private string password = "";
    private string domain = "";
    private string incomingCallNumber = "";
    private string defaultCallNumber = "";
    private string apiUsername = "";
    private string apiPassword = "";
    private SIPTransport? sipTransport;
    private SIPUDPChannel? sipChannel;
    private SIPRegistrationUserAgent? regUserAgent;
    private WindowsAudioEndPoint? winAudio;
    private VoIPMediaSession? voIPMediaSession;
    private SIPUserAgent? userAgent;
    private SIPServerUserAgent? incomingCall;
    private string? incomingCallerId;
    private SoundPlayer? ringPlayer;
    private HashSet<string> _seenIds = new HashSet<string>();
    private readonly CancellationTokenSource _cts = new CancellationTokenSource();

    public MainWindow()
    {
        InitializeComponent();

        // initialize the sip session
        InitializeSIPSession();

        // display the text
        DisplayBox.Text = defaultCallNumber;
        TitleText.Text = "SIP Dialer - " + incomingCallNumber;

        // start polling, cancelled automatically when the window closes
        Closed += (s, e) => _cts.Cancel();
        _ = StartPolling(_cts.Token);
    }

    /// <summary>
    /// Initializes the SIP session by loading configuration, setting up SIP transport,
    /// registering with the SIP server, configuring audio endpoints, and subscribing to SIP events.
    /// </summary>
    private void InitializeSIPSession()
    {
        // read the app settings
        // these are secrets
        var json = File.ReadAllText("appsettings.local.json");
        var config = JsonSerializer.Deserialize<Config>(json) ?? new Config();

        // load the app settings
        username = config.Voip.Username;
        password = config.Voip.Password;
        domain = config.Voip.Domain;
        incomingCallNumber = config.Voip.IncomingCallNumber;
        defaultCallNumber = config.Voip.DefaultCallNumber;
        apiUsername = config.Voip.ApiUsername;
        apiPassword = config.Voip.ApiPassword;

        // initialize the agent
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

        // start the windows audio
        winAudio = new WindowsAudioEndPoint(new AudioEncoder());
        winAudio.RestrictFormats(x => x.Codec == AudioCodecsEnum.PCMU);

        // start the media session
        voIPMediaSession = new VoIPMediaSession(winAudio.ToMediaEndPoints())
        {
            AcceptRtpFromAny = true
        };

        // create a user agent and connect events
        userAgent = new SIPUserAgent(sipTransport, null);
        userAgent.OnIncomingCall += UserAgent_OnIncomingCall;
        userAgent.OnCallHungup += UserAgent_OnCallHangup;
        userAgent.ServerCallCancelled += UserAgent_ServerCallCancelled;
    }

    /// <summary>
    /// Handles incoming call events. Displays the caller information, enables the answer button,
    /// and prepares the call for answering. Also plays a ring sound to notify the user.
    /// </summary>
    private void UserAgent_OnIncomingCall(SIPUserAgent agent, SIPRequest request)
    {
        Dispatcher.Invoke(() =>
        {
            // get the caller id of that is calling
            incomingCallerId = request.Header.From.FromURI.User;

            // indicate a call from the callerId
            DisplayBox.Text = $"CF {incomingCallerId}";

            // show the answer button as enabled
            AnswerButton.IsEnabled = true;
            AnswerButton.Background = new SolidColorBrush(Colors.Green);

            // accept the incoming call
            incomingCall = userAgent?.AcceptCall(request);

            // play the ring sound
            PlayRingSound();
        });
    }

    /// <summary>
    /// Handles call hangup events. Closes the media session, recreates audio and media endpoints,
    /// and resets the UI to prepare for the next call. Also stops any playing ring sound.
    /// </summary>
    private void UserAgent_OnCallHangup(SIPDialogue dialogue)
    {
        Dispatcher.Invoke(() =>
        {
            // stop the ring sound
            StopRingSound();

            // indicate that the call ended
            voIPMediaSession?.Close("call ended");

            // activte the windows audio
            winAudio = new WindowsAudioEndPoint(new AudioEncoder());
            winAudio.RestrictFormats(x => x.Codec == AudioCodecsEnum.PCMU);

            // create a media session
            voIPMediaSession = new VoIPMediaSession(winAudio.ToMediaEndPoints())
            {
                AcceptRtpFromAny = true
            };

            // indicate that the call was hung up
            incomingCall = null;
            DisplayBox.Clear();
            incomingCallerId = null;

            // show the answer button as not enabled
            AnswerButton.IsEnabled = false;
            AnswerButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6b8eff"));
        });
    }

    /// <summary>
    /// Handles call cancellation events when the remote party cancels an incoming call before it's answered.
    /// Displays the cancellation message, disables the answer button, and stops the ring sound.
    /// </summary>
    private void UserAgent_ServerCallCancelled(ISIPServerUserAgent uas, SIPRequest cancelRequest)
    {
        Dispatcher.Invoke(() =>
        {
            // stop the ring sound
            StopRingSound();

            // indicate that the call was cancelled and the number that tried to call
            DisplayBox.Text = $"Cancel {cancelRequest.Header.From.FromURI.User}";

            // show the answer buton and not enabled
            AnswerButton.IsEnabled = false;
            AnswerButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6b8eff"));
        });
    }

    /// <summary>
    /// Initiates an outgoing SIP call to the specified phone number using the configured credentials
    /// and media session. Enables the hangup button when the call is placed.
    /// </summary>
    private async Task<bool> MakeCall(string phoneNumber)
    {
        // set the URI for to and from to the phone number and callerId
        var toURI = SIPURI.ParseSIPURI($"sip:{phoneNumber}@{domain}");
        var fromURI = SIPURI.ParseSIPURI($"sip:{incomingCallNumber}@{domain}");

        // create a description of the call
        var callDescriptor = new SIPCallDescriptor(
            username,              // auth username
            password,              // auth password
            toURI.ToString(),      // destination
            fromURI.ToString(),    // caller ID
            null, null, null, null,
            SIPCallDirection.Out,
            null,
            null,
            null
        );

        // set the hangup button to enabled
        HangupButton.IsEnabled = true;
        HangupButton.Background = new SolidColorBrush(Colors.Green);

        // make the call
        return await userAgent!.Call(callDescriptor, voIPMediaSession);        
    }

    /// <summary>
    /// Handles dial button clicks. Appends the clicked digit to the display box for building phone numbers.
    /// </summary>
    private void DialButton_Click(object sender, RoutedEventArgs e)
    {
        // see if a dial button was pressed
        if (sender is Button button && button.Content is string digit)
        {
            // add the new digit
            DisplayBox.Text += digit;
        }
    }

    /// <summary>
    /// Clears the phone number displayed in the display box.
    /// </summary>
    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        // clear out the display
        DisplayBox.Clear();
    }

    /// <summary>
    /// Initiates an outgoing call to the phone number displayed in the display box.
    /// </summary>
    private async void CallButton_Click(object sender, RoutedEventArgs e)
    {
        // show the number that we are calling
        string phoneNumber = DisplayBox.Text;

        // check to see if this number is valid
        if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            // ask the user if it is ok to call this number
            MessageBox.Show($"Calling: {phoneNumber}", "SIP Call", MessageBoxButton.OK, MessageBoxImage.Information);
            // make the call
            bool callResult = await MakeCall(phoneNumber);
        }
    }

    private async void SendSMS_Click(object sender, RoutedEventArgs e)
    {
        // show the number that we are calling
        string phoneNumber = DisplayBox.Text;

        // check to see if this number is valid
        if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            await SMS.SendSMSViaVoipMs(
                apiUsername,
                apiPassword,
                incomingCallNumber,
                phoneNumber,
                "Hello from Michael Van Hulle"
            );
        }
    }

    /// <summary>
    /// Answers an incoming call. Sends the SIP 200 OK response, connects the media session,
    /// and enables the hangup button. Displays success or failure status. Stops the ring sound.
    /// </summary>
    private async void AnswerButton_Click(object sender, RoutedEventArgs e)
    {
        // check to see if we have an incoming call and a media session
        if (incomingCall != null && voIPMediaSession != null)
        {
            // stop the ring sound when answering
            StopRingSound();

            // show the answer buton as not enabled
            AnswerButton.IsEnabled = false;
            var result = await userAgent!.Answer(incomingCall, voIPMediaSession);

            // see if we were successful
            if (result)
            {
                // indicate the called number that was answered
                DisplayBox.Text = $"CA {incomingCallerId}";

                // show the handup button as enabled
                HangupButton.IsEnabled = true;
                HangupButton.Background = new SolidColorBrush(Colors.Green);
            }
            else
            {
                // indicate that the call failed and the number that it failed on
                DisplayBox.Text = $"F {incomingCallerId}";

                // show the answer button as enabled
                AnswerButton.IsEnabled = true;
                AnswerButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6b8eff"));
            }
        }
    }

    /// <summary>
    /// Terminates the current call by sending the SIP hangup request and disables the hangup button.
    /// </summary>
    private async void HangupButton_Click(object sender, RoutedEventArgs e)
    {
        // hangup the call
        userAgent?.Hangup();

        // show the hangup button as not enabled
        HangupButton.IsEnabled = false;
        HangupButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6b8eff"));
    }

    /// <summary>
    /// Plays a ring sound when an incoming call arrives. Uses the system beep sound repeated.
    /// </summary>
    private void PlayRingSound()
    {
        // stop any existing ring sound
        StopRingSound();

        // create a new sound player with the system notification sound
        ringPlayer = new SoundPlayer();

        // play the system asterisk sound as a ring notification
        SystemSounds.Asterisk.Play();

        // start a task to repeat the ring sound every 2 seconds
        Task.Run(async () =>
        {
            while (ringPlayer != null)
            {
                await Task.Delay(2000);
                if (ringPlayer != null)
                {
                    Dispatcher.Invoke(() =>
                    {
                        SystemSounds.Asterisk.Play();
                    });
                }
            }
        });
    }

    /// <summary>
    /// Stops the ring sound that was played for an incoming call.
    /// </summary>
    private void StopRingSound()
    {
        // dispose and clear the sound player
        ringPlayer?.Dispose();
        ringPlayer = null;
    }

    public async Task StartPolling(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var messages = await SMS.GetNewMessages(
                apiUsername, 
                apiPassword, 
                incomingCallNumber);

            foreach (var msg in messages)
            {
                // Skip messages we've already processed
                if (_seenIds.Contains(msg.Id))
                    continue;

                _seenIds.Add(msg.Id);

                Dispatcher.Invoke(() =>
                {
                    SmsMessagesBox.Text += $"[{msg.Date}] {msg.From}: {msg.Message}\n";
                });
            }

            await Task.Delay(TimeSpan.FromSeconds(30), ct); // poll every 30 seconds
        }
    }
}
