using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstogramApp.Services;
using PortAudioSharp;
using SIPSorceryMedia.FFmpeg;

namespace InstogramApp.ViewModels;

public partial class AudioDeviceViewModel : ViewModelBase
{
    public int    DeviceIndex { get; init; }
    public string Name       { get; init; } = "";
}

public partial class CameraDeviceViewModel : ViewModelBase
{
    public string Path { get; init; } = "";
    public string Name { get; init; } = "";
    public override string ToString() => Name;
}

public partial class SettingsViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;

    public ObservableCollection<AudioDeviceViewModel>  MicDevices     { get; } = [];
    public ObservableCollection<AudioDeviceViewModel>  SpeakerDevices { get; } = [];
    public ObservableCollection<CameraDeviceViewModel> CameraDevices  { get; } = [];

    [ObservableProperty] private AudioDeviceViewModel?  _selectedMic;
    [ObservableProperty] private AudioDeviceViewModel?  _selectedSpeaker;
    [ObservableProperty] private CameraDeviceViewModel? _selectedCamera;
    [ObservableProperty] private string _statusText = "";

    public SettingsViewModel(MainWindowViewModel main)
    {
        _main = main;
        LoadAudioDevices();
        LoadCameraDevices();
    }

    private void LoadAudioDevices()
    {
        try
        {
            PortAudio.Initialize();

            MicDevices.Add(new AudioDeviceViewModel    { DeviceIndex = -1, Name = "System Default" });
            SpeakerDevices.Add(new AudioDeviceViewModel{ DeviceIndex = -1, Name = "System Default" });

            for (int i = 0; i < PortAudio.DeviceCount; i++)
            {
                var info = PortAudio.GetDeviceInfo(i);
                if (info.maxInputChannels  > 0) MicDevices.Add(    new AudioDeviceViewModel { DeviceIndex = i, Name = info.name });
                if (info.maxOutputChannels > 0) SpeakerDevices.Add(new AudioDeviceViewModel { DeviceIndex = i, Name = info.name });
            }

            PortAudio.Terminate();

            SelectedMic     = FindAudio(MicDevices,     AppState.Instance.MicDeviceIndex)     ?? MicDevices[0];
            SelectedSpeaker = FindAudio(SpeakerDevices, AppState.Instance.SpeakerDeviceIndex) ?? SpeakerDevices[0];
        }
        catch
        {
            StatusText = "Could not enumerate audio devices.";
        }
    }

    private void LoadCameraDevices()
    {
        try
        {
            FFmpegInit.Initialise(FfmpegLogLevelEnum.AV_LOG_FATAL,
                "/usr/lib/x86_64-linux-gnu");

            CameraDevices.Add(new CameraDeviceViewModel { Path = "", Name = "None" });

            var cams = FFmpegCameraManager.GetCameraDevices();
            if (cams != null)
                foreach (var c in cams)
                    CameraDevices.Add(new CameraDeviceViewModel { Path = c.Path, Name = c.Name });

            SelectedCamera = FindCamera(AppState.Instance.CameraDevicePath) ?? CameraDevices[0];
        }
        catch
        {
            CameraDevices.Add(new CameraDeviceViewModel { Path = "", Name = "Camera unavailable" });
            SelectedCamera = CameraDevices[0];
        }
    }

    private static AudioDeviceViewModel? FindAudio(ObservableCollection<AudioDeviceViewModel> list, int index)
    {
        foreach (var d in list) if (d.DeviceIndex == index) return d;
        return null;
    }

    private CameraDeviceViewModel? FindCamera(string path)
    {
        foreach (var d in CameraDevices) if (d.Path == path) return d;
        return null;
    }

    [RelayCommand]
    void Save()
    {
        AppState.Instance.MicDeviceIndex     = SelectedMic?.DeviceIndex     ?? -1;
        AppState.Instance.SpeakerDeviceIndex = SelectedSpeaker?.DeviceIndex ?? -1;
        AppState.Instance.CameraDevicePath   = SelectedCamera?.Path         ?? "";
        StatusText = "Saved.";
    }

    [RelayCommand]
    void Back() => _main.GoFeedCommand.Execute(null);
}
