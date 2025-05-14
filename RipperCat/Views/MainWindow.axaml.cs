using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RipperCat.Models;

namespace RipperCat.Views;

public sealed partial class MainWindow : Window
{
    private readonly AudioRecorder _recorder = new();
    private CancellationTokenSource? _cts;
    private string _basePath = "";
    private int _fileIndex;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => DeviceCombo.ItemsSource = _recorder.ListInputDevices();

        BrowseButton.Click += BrowseAsync;
        StartButton.Click  += StartAsync;
        BreakButton.Click  += (_, _) => _recorder.RequestSongBreak();
        StopButton.Click   += (_, _) => _cts?.Cancel();
    }

    private async void BrowseAsync(object? s, RoutedEventArgs e)
    {
        var fmt = SelectedFormat();
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = "session",
            FileTypeChoices = new[] { new FilePickerFileType(fmt == AudioFormat.Opus ? "Opus audio" : "MP3 audio")
                { Patterns = new[] { fmt == AudioFormat.Opus ? "*.opus" : "*.mp3" } } }

        });
        if (file != null)
        {
            _basePath = Path.Combine(Path.GetDirectoryName(file.Path.LocalPath)!,
                                      Path.GetFileNameWithoutExtension(file.Path.LocalPath));
            PathBox.Text = _basePath;
        }
    }

    private async void StartAsync(object? s, RoutedEventArgs e)
    {
        if (DeviceCombo.SelectedItem is not AudioRecorder.AudioDeviceInfo dev) return;
        if (string.IsNullOrWhiteSpace(_basePath))
        {
            await ShowMessage("Choose a base output file first.");
            return;
        }

        // ✅ read once on UI thread
        var format = SelectedFormat();         

        ToggleButtons(true);
        _fileIndex = 0;
        StatusText.Text = "Recording 1…";

        _cts = new CancellationTokenSource();

        // pass captured 'format' and a next-path factory that uses *only* local data
        _ = Task.Run(() =>
                _recorder.RecordContinuously(
                    dev,
                    NextPathLocal,          // ← no UI access inside
                    format,
                    _cts.Token))
            .ContinueWith(_ => Dispatcher.UIThread.Post(() => ToggleButtons(false)));

        string NextPathLocal()
        {
            _fileIndex++;
            var ext = format == AudioFormat.Opus ? ".opus" : ".mp3";
            var path = $"{_basePath}_{_fileIndex}{ext}";
            Dispatcher.UIThread.Post(() => StatusText.Text = $"Recording {_fileIndex}…");
            return path;
        }
    }


    private string NextPath()
    {
        _fileIndex++;
        var ext = SelectedFormat() == AudioFormat.Opus ? ".opus" : ".mp3";
        var path = $"{_basePath}_{_fileIndex}{ext}";
        Dispatcher.UIThread.Post(() => StatusText.Text = $"Recording {_fileIndex}…");
        return path;
    }

    private void ToggleButtons(bool rec)
    {
        StartButton.IsEnabled  = !rec;
        StopButton.IsEnabled   = BreakButton.IsEnabled = rec;
        if (!rec) StatusText.Text = "Ready";
    }

    private AudioFormat SelectedFormat() =>
        (FormatCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "Mp3"
            ? AudioFormat.Mp3
            : AudioFormat.Opus;
    
    private async Task ShowMessage(string msg) => await MessageBox(msg);
    
    private async Task MessageBox(string msg)
    {
        await new Window
        {
            Content = new StackPanel { Children = { new TextBlock { Text = msg } } },
            Width = 320,
            Height = 140,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        }.ShowDialog(this);
    }
    
}