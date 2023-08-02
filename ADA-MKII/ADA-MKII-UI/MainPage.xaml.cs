using System.Globalization;
using System.Threading.Tasks;
using System.Threading;
using System;
using CommunityToolkit.Maui.Media;
using CommunityToolkit.Maui.Alerts;

namespace ADA_MKII_UI
{
    public partial class MainPage : ContentPage
    {
        int count = 0;

        public MainPage()
        {
            InitializeComponent();

            TextToSpeech.Default.SpeakAsync("Active");
        }

        private void OnCounterClicked(object sender, EventArgs e)
        {
            count++;

            if (count == 1)
                CounterBtn.Text = $"Clicked {count} time";
            else
                CounterBtn.Text = $"Clicked {count} times";

            TextToSpeech.Default.SpeakAsync(CounterBtn.Text);

            SemanticScreenReader.Announce(CounterBtn.Text);
        }

        private void btnSettings_Clicked(object sender, EventArgs e)
        {
            Shell.Current.GoToAsync("Settings");
        }

        public string RecognitionText { get; set; }

        private async Task Listen()
        {
            var isGranted = await SpeechToText.Default.RequestPermissions(CancellationToken.None);
            if (!isGranted)
            {
                await Toast.Make("Permission not granted").Show(CancellationToken.None);
                return;
            }
            var recognitionResult = await SpeechToText.Default.ListenAsync(
                                                CultureInfo.GetCultureInfo("en-au"),
                                                new Progress<string>(partialText =>
                                                {
                                                    RecognitionText += partialText + " ";
                                                }), CancellationToken.None);
            if (recognitionResult.IsSuccessful)
            {
                RecognitionText = recognitionResult.Text;

                await TextToSpeech.Default.SpeakAsync(recognitionResult.Text);
            }
            else
            {
                await Toast.Make(recognitionResult.Exception?.Message ?? "Unable to recognize speech").Show(CancellationToken.None);
            }
        }

        private void btnListen_Clicked(object sender, EventArgs e)
        {
            _ = Listen();
        }
    }
}