using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstogramApp.Services;
using PortAudioSharp;

namespace InstogramApp.ViewModels;

public partial class CallViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;
    private readonly Guid                _remoteUserId;

    // ── State ──────────────────────────────────────────────────────────────────
    [ObservableProperty] private string _statusText    = "Connecting…";
    [ObservableProperty] private string _remoteName    = "";
    [ObservableProperty] private bool   _isMuted;
    [ObservableProperty] private bool   _isNotMuted    = true;
    [ObservableProperty] private bool   _isCallActive;
    [ObservableProperty] private bool   _isIncoming;
    [ObservableProperty] private bool   _isOutgoing;
    [ObservableProperty] private string _sdpOffer      = "";

    // ── PortAudio streams ──────────────────────────────────────────────────────
    private Stream? _inputStream;
    private Stream? _outputStream;

    // Simple ring buffer for audio exchange (local loopback for now)
    private readonly byte[] _audioBuffer = new byte[65536];
    private int _bufferWrite;
    private int _bufferRead;

    // ── Outgoing call constructor ──────────────────────────────────────────────
    public CallViewModel(MainWindowViewModel main, Guid remoteUserId, string remoteName)
    {
        _main         = main;
        _remoteUserId = remoteUserId;
        RemoteName    = remoteName;
        IsOutgoing    = true;
        StatusText    = $"Calling {remoteName}…";

        SubscribeToSignalling();
        _ = StartOutgoingCallAsync();
    }

    // ── Incoming call constructor ──────────────────────────────────────────────
    public CallViewModel(MainWindowViewModel main, Guid callerId, string callerName, string sdpOffer)
    {
        _main         = main;
        _remoteUserId = callerId;
        RemoteName    = callerName;
        SdpOffer      = sdpOffer;
        IsIncoming    = true;
        StatusText    = $"Incoming call from {callerName}";

        SubscribeToSignalling();
    }

    private void SubscribeToSignalling()
    {
        ServerClient.Instance.OnCallAnswered += OnCallAnswered;
        ServerClient.Instance.OnCallEnded    += OnCallEnded;
        ServerClient.Instance.OnIceCandidate += OnIceCandidate;
    }

    private void UnsubscribeSignalling()
    {
        ServerClient.Instance.OnCallAnswered -= OnCallAnswered;
        ServerClient.Instance.OnCallEnded    -= OnCallEnded;
        ServerClient.Instance.OnIceCandidate -= OnIceCandidate;
    }

    // ── Outgoing call flow ─────────────────────────────────────────────────────

    private async Task StartOutgoingCallAsync()
    {
        // In a real WebRTC implementation, you'd create an RTCPeerConnection and
        // generate a real SDP offer. Here we send a placeholder that both sides
        // use as the signal to start PortAudio streams.
        var fakeSdp = $"instogram-offer:{Guid.NewGuid()}";
        await ServerClient.Instance.CallUserAsync(_remoteUserId, fakeSdp);
    }

    // ── Accept / Reject ────────────────────────────────────────────────────────

    [RelayCommand]
    async Task Accept()
    {
        StatusText = "Connecting…";
        var fakeAnswer = $"instogram-answer:{Guid.NewGuid()}";
        await ServerClient.Instance.CallAnswerAsync(_remoteUserId, fakeAnswer);
        StartAudio();
        IsCallActive = true;
        IsIncoming   = false;
        StatusText   = $"In call with {RemoteName}";
    }

    [RelayCommand]
    async Task Decline()
    {
        await ServerClient.Instance.HangUpAsync(_remoteUserId);
        NavigateBack();
    }

    // ── In-call controls ───────────────────────────────────────────────────────

    [RelayCommand]
    void ToggleMute()
    {
        IsMuted    = !IsMuted;
        IsNotMuted = !IsMuted;
        // Would pause/resume input stream in a real implementation
    }

    [RelayCommand]
    async Task HangUp()
    {
        await ServerClient.Instance.HangUpAsync(_remoteUserId);
        StopAudio();
        NavigateBack();
    }

    // ── Signalling callbacks ───────────────────────────────────────────────────

    private void OnCallAnswered(Guid answererId, string sdpAnswer)
    {
        if (answererId != _remoteUserId) return;
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            StartAudio();
            IsCallActive = true;
            IsOutgoing   = false;
            StatusText   = $"In call with {RemoteName}";
        });
    }

    private void OnIceCandidate(Guid fromId, string candidate)
    {
        // In real WebRTC, we'd add this ICE candidate to the peer connection
    }

    private void OnCallEnded(Guid fromId)
    {
        if (fromId != _remoteUserId) return;
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            StopAudio();
            StatusText = "Call ended";
            NavigateBack();
        });
    }

    // ── PortAudio ──────────────────────────────────────────────────────────────

    private void StartAudio()
    {
        try
        {
            PortAudio.Initialize();

            var inputParams = new StreamParameters
            {
                device              = PortAudio.DefaultInputDevice,
                channelCount        = 1,
                sampleFormat        = SampleFormat.Int16,
                suggestedLatency    = PortAudio.GetDeviceInfo(PortAudio.DefaultInputDevice).defaultLowInputLatency
            };

            var outputParams = new StreamParameters
            {
                device              = PortAudio.DefaultOutputDevice,
                channelCount        = 1,
                sampleFormat        = SampleFormat.Int16,
                suggestedLatency    = PortAudio.GetDeviceInfo(PortAudio.DefaultOutputDevice).defaultLowOutputLatency
            };

            const double sampleRate = 16000;
            const uint   framesPerBuffer = 256;

            // Capture callback — would send audio over network in real impl
            _inputStream = new Stream(
                inParams:        inputParams,
                outParams:       null,
                sampleRate:      sampleRate,
                framesPerBuffer: framesPerBuffer,
                streamFlags:     StreamFlags.ClipOff,
                callback:        CaptureCallback,
                userData:        IntPtr.Zero);
            _inputStream.Start();

            // Playback callback — would receive audio from network in real impl
            _outputStream = new Stream(
                inParams:        null,
                outParams:       outputParams,
                sampleRate:      sampleRate,
                framesPerBuffer: framesPerBuffer,
                streamFlags:     StreamFlags.ClipOff,
                callback:        PlaybackCallback,
                userData:        IntPtr.Zero);
            _outputStream.Start();
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.InvokeAsync(() => StatusText = $"Audio error: {ex.Message}");
        }
    }

    private StreamCallbackResult CaptureCallback(
        nint input, nint output, uint frameCount,
        ref StreamCallbackTimeInfo timeInfo, StreamCallbackFlags statusFlags, nint userData)
    {
        if (IsMuted || input == IntPtr.Zero) return StreamCallbackResult.Continue;

        unsafe
        {
            var src  = new Span<short>((void*)input, (int)frameCount);
            var bytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(src);
            int write = _bufferWrite;
            foreach (var b in bytes)
            {
                _audioBuffer[write % _audioBuffer.Length] = b;
                write++;
            }
            _bufferWrite = write;
        }
        return StreamCallbackResult.Continue;
    }

    private StreamCallbackResult PlaybackCallback(
        nint input, nint output, uint frameCount,
        ref StreamCallbackTimeInfo timeInfo, StreamCallbackFlags statusFlags, nint userData)
    {
        if (output == IntPtr.Zero) return StreamCallbackResult.Continue;

        unsafe
        {
            var dst   = new Span<byte>((void*)output, (int)frameCount * 2);
            int read  = _bufferRead;
            for (int i = 0; i < dst.Length; i++)
            {
                if (read < _bufferWrite)
                    dst[i] = _audioBuffer[read++ % _audioBuffer.Length];
                else
                    dst[i] = 0;
            }
            _bufferRead = read;
        }
        return StreamCallbackResult.Continue;
    }

    private void StopAudio()
    {
        try
        {
            _inputStream?.Stop();
            _inputStream?.Dispose();
            _outputStream?.Stop();
            _outputStream?.Dispose();
            PortAudio.Terminate();
        }
        catch { }
        finally
        {
            _inputStream  = null;
            _outputStream = null;
        }
    }

    private void NavigateBack()
    {
        UnsubscribeSignalling();
        StopAudio();
        Dispatcher.UIThread.InvokeAsync(() => _main.Navigate(new DMListViewModel(_main)));
    }
}
