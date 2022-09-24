// MainPage

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;

// Документацию по шаблону элемента "Пустая страница" см. по адресу https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x419

namespace ShazamApp
{
    /// <summary>
    /// Пустая страница, которую можно использовать саму по себе или для перехода внутри фрейма.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
        }

        void ShazamProcess()
        {
               
            TextBox.Text = "Listening... ";

            try
            {
                var result = CaptureAndTag();

                if (result.Success)
                {
                    //Console.CursorLeft = 0;
                    //Console.WriteLine(result.Url);
                    TextBox.Text = "Sucess : " + result.Url;
                    //Process.Start("explorer", result.Url);
                }
                else
                {
                    TextBox.Text = "Nothing " + ":(";
                }
            }
            catch (Exception x)
            {
                TextBox.Text = "error: " + x.Message;
            }               

        }//

        //
        static ShazamResult CaptureAndTag()
        {
            var analysis = new Analysis();
            var finder = new LandmarkFinder(analysis);

            using (var capture = new WasapiCapture())
            {
                var captureBuf = new BufferedWaveProvider(capture.WaveFormat)
                {
                    ReadFully = false
                };

                capture.DataAvailable += (s, e) =>
                {
                    try
                    {
                        captureBuf.AddSamples(e.Buffer, 0, e.BytesRecorded);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("[ex] Exception : " + ex.Message);
                    }
                };

                capture.StartRecording();

                using (var resampler = new MediaFoundationResampler(captureBuf, new WaveFormat(Analysis.SAMPLE_RATE, 16, 1)))
                {
                    var sampleProvider = resampler.ToSampleProvider();
                    var retryMs = 3000;
                    var tagId = Guid.NewGuid().ToString();

                    while (true)
                    {
                        while (captureBuf.BufferedDuration.TotalSeconds < 1)
                            Thread.Sleep(100);

                        analysis.ReadChunk(sampleProvider);

                        if (analysis.StripeCount > 2 * LandmarkFinder.RADIUS_TIME)
                            finder.Find(analysis.StripeCount - LandmarkFinder.RADIUS_TIME - 1);

                        if (analysis.ProcessedMs >= retryMs)
                        {
                            //new Painter(analysis, finder).Paint("c:/temp/spectro.png");
                            //new Synthback(analysis, finder).Synth("c:/temp/synthback.raw");

                            var sigBytes = Sig.Write(Analysis.SAMPLE_RATE, analysis.ProcessedSamples, finder);
                            var result = ShazamApi.SendRequest(tagId, analysis.ProcessedMs, sigBytes).GetAwaiter().GetResult();
                            if (result.Success)
                                return result;

                            retryMs = result.RetryMs;
                            if (retryMs == 0)
                                return result;
                        }
                    }
                }
            }
        }//

        private void Shazam_Click(object sender, RoutedEventArgs e)
        {
            ShazamProcess();
        }
    }
}
