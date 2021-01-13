using Plugin.Media;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Essentials;
using Xamarin.Forms;
using VLC = LibVLCSharp.Shared;

namespace App5
{
    public partial class MainPage : ContentPage
    {
        private VLC.LibVLC _libVLC;

        private StringBuilder _log = new StringBuilder();

        public MainPage()
        {
            var liboptions = new string[]
            {
                $"--no-osd",
                $"--no-spu",
                $"--sout-file-overwrite",
                //$"--network-caching=1200",
                //$"--rtsp-tcp"
            };

            VLC.Core.Initialize();
            _libVLC = new VLC.LibVLC(false, liboptions);
            _libVLC.Log += this.LibVLC_Log;

            InitializeComponent();
        }

        private async void TakeVideoButton_Clicked(object sender, EventArgs e)
        {
            try
            {
                string fileName = $"video_{DateTime.UtcNow:yyyyMMddHHmmss}";
                var video = await CrossMedia.Current.TakeVideoAsync(new Plugin.Media.Abstractions.StoreVideoOptions
                {
                    Name = fileName,
                    DesiredLength = new System.TimeSpan(0, 0, 20),
                    Quality = Plugin.Media.Abstractions.VideoQuality.High,
                    SaveToAlbum = false,
                    SaveMetaData = true,
                    RotateImage = false

                });

                string sourceVideoPath = video.Path;

                using (var media = new VLC.Media(_libVLC, sourceVideoPath, VLC.FromType.FromPath))
                {
                    FileInfo fileInfo = new FileInfo(sourceVideoPath);
                    await media.Parse();
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine($"Video length is: {TimeSpan.FromMilliseconds(media.Duration).TotalSeconds}sec");
                    sb.AppendLine($"File size is: {fileInfo.Length / 1024f / 1024f}mb");
                    this.InfoLabel.Text = sb.ToString();

                    await this.DisplayAlert("Converting", "Tap on ok to proceed with transcoding", "Ok");

                    Log("Starting transcoding...");

                    SemaphoreSlim procSemaphore = new SemaphoreSlim(1);
                    string convertedVideoPath = GetNewVideoEmptyFile();
                    if (Device.RuntimePlatform == Device.Android)
                    {
                        media.AddOption($":sout=#transcode{{vcodec=h264,venc=x264{{cfr=40}},vb=2048,scale=auto,acodec=mp4a,ab=96,channels=2,samplerate=44100}}:std{{access=file,mux=ts,dst={convertedVideoPath}}}");
                    }
                    else if (Device.RuntimePlatform == Device.iOS)
                    {
                        media.AddOption($":sout=#transcode{{vcodec=h264,venc={{module=avcodec{{codec=h264_videotoolbox}}, vcodec=h264}},vb=2048,scale=auto,acodec=mp4a,ab=96,channels=2,samplerate=44100,scodec=none}}:std{{access=file,mux=mp4,dst={convertedVideoPath}}}");
                    }

                    using (VLC.MediaPlayer mediaPlayer = new VLC.MediaPlayer(_libVLC))
                    {
                        mediaPlayer.EnableHardwareDecoding = false;
                        mediaPlayer.Media = media;

                        mediaPlayer.EncounteredError += (s, ea) =>
                        {
                            System.Diagnostics.Debug.WriteLine("Something happened during conversion.");
                        };

                        mediaPlayer.Playing += (s, ea) =>
                        {
                            System.Diagnostics.Debug.WriteLine("Converting...");
                            Device.InvokeOnMainThreadAsync(() => this.InfoLabel.Text += $"{Environment.NewLine}Converting...");
                        };

                        mediaPlayer.Stopped += (s, ea) =>
                        {
                            procSemaphore.Release();
                            System.Diagnostics.Debug.WriteLine("Conversion Completed!");
                            Device.InvokeOnMainThreadAsync(() => this.InfoLabel.Text += $"{Environment.NewLine}Conversion completed!");
                        };

                        await procSemaphore.WaitAsync();
                        System.Diagnostics.Debug.WriteLine($"Is background thread? {Thread.CurrentThread.IsBackground}");
                        mediaPlayer.Play();
                        await procSemaphore.WaitAsync();
                    }

                    Log("Transcoding completed!");
                    string fullLog = _log.ToString();
                    System.Diagnostics.Debug.WriteLine(fullLog);

                    FileInfo convertedFileInfo = new FileInfo(convertedVideoPath);
                    var convertedMedia = new VLC.Media(_libVLC, convertedVideoPath, VLC.FromType.FromPath);
                    await convertedMedia.Parse();
                    StringBuilder sb2 = new StringBuilder();
                    sb2.AppendLine($"Video length is: {TimeSpan.FromMilliseconds(convertedMedia.Duration).TotalSeconds}sec");
                    sb2.AppendLine($"File size is: {convertedFileInfo.Length / 1024f / 1024f}mb");
                    this.InfoLabel.Text = sb2.ToString();

                    using (Stream stream = File.OpenRead(convertedVideoPath))
                    {
                        var exifData = VideoHelper.GetEXIFData(stream);
                        var exifTags = VideoHelper.GetEXIFTags(exifData, "Matrix");
                    }

                    await Share.RequestAsync(new ShareFileRequest
                    {
                        Title = Title,
                        File = new ShareFile(convertedVideoPath)
                    });

                    if (File.Exists(sourceVideoPath))
                        File.Delete(sourceVideoPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                await DisplayAlert("Error", "Something wend bad", "Ok");
            }
        }

        private void LibVLC_Log(object sender, VLC.LogEventArgs e)
        {
            Log(e.FormattedLog);
        }

        private string GetNewVideoEmptyFile()
        {
            string videoPath = Path.GetTempFileName();
            File.Copy(videoPath, videoPath.Replace(".tmp", ".mp4"));
            File.Delete(videoPath);
            videoPath = videoPath.Replace(".tmp", ".mp4");
            return videoPath;
        }

        private void Log(string message)
        {
            _log.AppendLine($"{DateTime.Now.ToString("dd-MM-yy hh:mm:ss")} - {message}");
        }

    }
}
