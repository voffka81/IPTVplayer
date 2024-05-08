
namespace TV_Player.MAUI;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Timers;


public partial class PlayerPage : ContentPage
{
   
    private const string StreamUrl = "http://ost.da-tv.vip/uPVtzdGJfdG9rZW5dIiwibCI6ImE3MWU3N2ZhIiwicCI6ImE3MWU3N2ZhODM1YjMyMTYiLCJjIjoiOTcyIiwidCI6ImUzNjAwZTEwZmFmMGVhYjhhYWY1YTU2YzRkN2VjZTE5IiwiZCI6IjIzMTQ2IiwiciI6IjIzMDM4IiwibSI6InR2IiwiZHQiOiIwIn0eyJ1IjoiaHR0cDovLzQ1LjkzLjQ2LjI3Ojg4ODcvODQwMC92aWRlby5tM3U4P3Rva2V/tracks-v1a1/mono.m3u8?cid=972&did=23146&m=1&rid=23038&token=e3600e10faf0eab8aaf5a56c4d7ece19";
    //MediaPlayer _mediaPlayer;
    private const int RefreshIntervalMs = 1000; // Refresh interval in milliseconds
    private readonly string tempFilePath;
    private Timer timer;

    public PlayerPage()
    {
        InitializeComponent();

        tempFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "temp_media.mp4");
        StartStreaming();
    }

    private void StartStreaming()
    {
        try
        {
            // Initialize the timer
            timer = new Timer(RefreshIntervalMs);
            timer.Elapsed += async (sender, e) => await UpdateTempFile();
            timer.AutoReset = true;
            timer.Start();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error starting stream: {ex.Message}");
        }
    }

    private async Task UpdateTempFile()
    {
        try
        {
            // Download the stream data
            byte[] streamData = await DownloadStreamData(StreamUrl);

            // Write the stream data to the temporary file
            if (streamData != null)
            {
                await File.WriteAllBytesAsync(tempFilePath, streamData);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating temporary file: {ex.Message}");
        }
    }

    private async Task<byte[]> DownloadStreamData(string streamUrl)
    {
        using (HttpClient client = new HttpClient())
        {
            // Download the stream asynchronously
            return await client.GetByteArrayAsync(streamUrl);
        }
    }
}
