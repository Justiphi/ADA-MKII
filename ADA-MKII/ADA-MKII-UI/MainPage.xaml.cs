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
        private CancellationTokenSource tokenSource = new CancellationTokenSource();

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
        }

        private void btnSettings_Clicked(object sender, EventArgs e)
        {
            Shell.Current.GoToAsync("Settings");
        }

        public string RecognitionText { get; set; }

        private async Task Listen()
        {
            tokenSource = new CancellationTokenSource();
            RecognitionText = string.Empty;

            var isGranted = await SpeechToText.Default.RequestPermissions(tokenSource.Token);
            if (!isGranted)
            {
                await Toast.Make("Permission not granted").Show(tokenSource.Token);
                return;
            }


            var recognitionResult = await SpeechToText.Default.ListenAsync(
                CultureInfo.GetCultureInfo("en-us"),
                new Progress<string>(partialText =>
                {
                    RecognitionText += partialText + " ";
#if WINDOWS
    tokenSource.CancelAfter(500);
#endif
                }), tokenSource.Token);

            if (recognitionResult.IsSuccessful || recognitionResult.Exception is TaskCanceledException)
            {
                if (!string.IsNullOrEmpty(RecognitionText))
                {
                    await TextToSpeech.Default.SpeakAsync(RecognitionText);
                }
            }
            else
            {
                await Toast.Make(recognitionResult.Exception?.Message ?? "Unable to recognize speech").Show(tokenSource.Token);
            }
        }

        private void btnListen_Clicked(object sender, EventArgs e)
        {
            _ = Listen();
        }

        private void btnStopListen_Clicked(object sender, EventArgs e)
        {
            tokenSource?.Cancel();
        }
    }
}