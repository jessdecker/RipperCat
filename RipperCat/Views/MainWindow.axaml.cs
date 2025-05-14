using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

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
        Loaded          += OnLoaded;

        BrowseButton.Click += OnBrowseClicked;
        StartButton.Click  += OnStartClicked;
        BreakButton.Click  += (_, _) => _recorder.RequestSongBreak();
        StopButton.Click   += (_, _) => _cts?.Cancel();
    }

    // ---------- FIX 1: use ItemsSource instead of Items -------------
    private void OnLoaded(object? sender, EventArgs e) =>
        DeviceCombo.ItemsSource = _recorder.ListInputDevices();   // ✅ no Clear/Add needed

    // ---------------------------------------------------------------
    private async void OnBrowseClicked(object? sender, RoutedEventArgs e)
    {
        var save = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = "album",
            FileTypeChoices   = new[] { new FilePickerFileType("Opus audio") { Patterns = new[] { "*.opus" } } }
        });
        if (save != null)
        {
            _basePath = Path.Combine(
                Path.GetDirectoryName(save.Path.LocalPath)!,
                Path.GetFileNameWithoutExtension(save.Path.LocalPath));
            PathBox.Text = _basePath;
        }
    }

    private async void OnStartClicked(object? s, RoutedEventArgs e)
    {
        if (DeviceCombo.SelectedItem is not AudioRecorder.AudioDeviceInfo dev) return;
        if (string.IsNullOrWhiteSpace(_basePath))
        {
            await ShowMessage("Choose a base output file first.");   // ✅ FIX 2: ShowMessage restored
            return;
        }

        ToggleButtons(true);
        _fileIndex = 0;
        StatusText.Text = "Recording 1…";

        _cts = new CancellationTokenSource();
        _ = Task.Run(() => _recorder.RecordContinuously(dev, NextPath, _cts.Token))
                .ContinueWith(_ => Dispatcher.UIThread.Post(() => ToggleButtons(false)));
    }

    private string NextPath()
    {
        _fileIndex++;
        var path = $"{_basePath}_{_fileIndex}.opus";
        Dispatcher.UIThread.Post(() => StatusText.Text = $"Recording {_fileIndex}…");
        return path;
    }

    private void ToggleButtons(bool recording)
    {
        StartButton.IsEnabled  = !recording;
        StopButton.IsEnabled   = BreakButton.IsEnabled = recording;
        if (!recording) StatusText.Text = "Ready";
    }

    // ---------- FIX 2: MessageBox helper back ----------------------
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
