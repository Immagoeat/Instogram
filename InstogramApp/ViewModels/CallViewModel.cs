using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Concentus;
using Concentus.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InstogramApp.Services;
using PortAudioSharp;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.FFmpeg;

namespace InstogramApp.ViewModels;

public partial class CallViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _main;
    private readonly Guid                _remoteUserId;

    [ObservableProperty] private string _statusText   = "Connecting…";
    [ObservableProperty] private string _remoteName   = "";
    [ObservableProperty] private bool   _isMuted;
    [ObservableProperty] private bool   _isNotMuted   = true;
    [ObservableProperty] private bool   _isCallActive;
    [ObservableProperty] private bool   _isIncoming;
    [ObservableProperty] private bool   _isOutgoing;
    [ObservableProperty] private bool   _isCameraOn;
    [ObservableProperty] private bool   _isCameraOff  = true;
    [ObservableProperty] private bool   _hasRemoteVideo;
    [ObservableProperty] private WriteableBitmap? _localFrame;
    [ObservableProperty] private WriteableBitmap? _remoteFrame;

    private string _sdpOffer = "";

    private RTCPeerConnection?   _pc;
    private FFmpegCameraSource?  _camera;
    private FFmpegVideoEndPoint? _videoSink;
    private Stream?              _captureStream;
    private Stream?              _playbackStream;
    private bool                 _cleanedUp;
    private bool                 _paInitialised;

    // ── Opus ───────────────────────────────────────────────────────────────────
    private const int OpusRate      = 48000;
    private const int OpusChannels  = 1;
    private const int OpusFrameSize = 960;  // 20ms @ 48kHz
    private const int OpusPT        = 111;

    private readonly IOpusEncoder _opusEnc;
    private readonly IOpusDecoder _opusDec;

    // Capture ring buffer (PCM16 48kHz mono from mic)
    private readonly short[] _capBuf  = new short[OpusRate * 2];
    private int _capWrite;
    private int _capRead;

    // Playback ring buffer (decoded PCM16 48kHz mono)
    private readonly short[] _playBuf = new short[OpusRate * 4];
    private int _playWrite;
    private int _playRead;


    // ── Constructors ───────────────────────────────────────────────────────────
    public CallViewModel(MainWindowViewModel main, Guid remoteUserId, string remoteName)
    {
        (_opusEnc, _opusDec) = MakeCodecs();
        _main         = main;
        _remoteUserId = remoteUserId;
        RemoteName    = remoteName;
        IsOutgoing    = true;
        StatusText    = $"Calling {remoteName}…";
        SubscribeSignalling();
        _ = StartOutgoingCallAsync();
    }

    public CallViewModel(MainWindowViewModel main, Guid callerId, string callerName, string sdpOffer)
    {
        (_opusEnc, _opusDec) = MakeCodecs();
        _main         = main;
        _remoteUserId = callerId;
        RemoteName    = callerName;
        _sdpOffer     = sdpOffer;
        IsIncoming    = true;
        StatusText    = $"Incoming call from {callerName}";
        SubscribeSignalling();
    }

    private static (IOpusEncoder, IOpusDecoder) MakeCodecs()
    {
        var enc = OpusCodecFactory.CreateEncoder(OpusRate, OpusChannels, OpusApplication.OPUS_APPLICATION_VOIP);
        enc.Bitrate    = 64000;
        enc.Complexity = 5;
        return (enc, OpusCodecFactory.CreateDecoder(OpusRate, OpusChannels));
    }

    // ── Signalling ─────────────────────────────────────────────────────────────
    private void SubscribeSignalling()
    {
        ServerClient.Instance.OnCallAnswered += OnCallAnswered;
        ServerClient.Instance.OnCallEnded    += OnCallEnded;
        ServerClient.Instance.OnIceCandidate += OnRemoteIceCandidate;
    }

    private void UnsubscribeSignalling()
    {
        ServerClient.Instance.OnCallAnswered -= OnCallAnswered;
        ServerClient.Instance.OnCallEnded    -= OnCallEnded;
        ServerClient.Instance.OnIceCandidate -= OnRemoteIceCandidate;
    }

    // ── Peer connection ────────────────────────────────────────────────────────
    private RTCPeerConnection CreatePeerConnection()
    {
        var pc = new RTCPeerConnection(new RTCConfiguration
        {
            iceServers = new List<RTCIceServer>
            {
                new RTCIceServer { urls = "stun:stun.l.google.com:19302" }
            }
        });

        // Opus audio track
        pc.addTrack(new MediaStreamTrack(
            new AudioFormat(OpusPT, "OPUS", OpusRate, OpusChannels, null),
            MediaStreamStatusEnum.SendRecv));

        // VP8 video track
        pc.addTrack(new MediaStreamTrack(
            new List<VideoFormat> { new VideoFormat(VideoCodecsEnum.VP8, 96, 90000, null) },
            MediaStreamStatusEnum.SendRecv));

        // Received audio: decode Opus → playback buffer
        pc.OnRtpPacketReceived += (_, media, pkt) =>
        {
            if (media != SDPMediaTypesEnum.audio) return;
            var payload = pkt.Payload;
            if (payload == null || payload.Length == 0) return;
            try
            {
                var decoded = new short[OpusFrameSize * 2];
                int n = _opusDec.Decode(payload.AsSpan(), decoded.AsSpan(), OpusFrameSize, false);
                for (int i = 0; i < n; i++)
                {
                    _playBuf[_playWrite % _playBuf.Length] = decoded[i];
                    _playWrite++;
                }
            }
            catch { }
        };

        // Received video: decode VP8 via FFmpegVideoEndPoint → render
        _videoSink = new FFmpegVideoEndPoint();
        _videoSink.SetVideoSinkFormat(new VideoFormat(VideoCodecsEnum.VP8, 96, 90000, null));
        _videoSink.OnVideoSinkDecodedSample += OnRemoteVideoDecoded;
        pc.OnVideoFrameReceived += (ep, ts, frame, fmt) =>
            _videoSink.GotVideoFrame(ep, ts, frame, fmt);

        pc.onicecandidate += candidate =>
        {
            if (candidate?.candidate != null)
                _ = ServerClient.Instance.SendIceCandidateAsync(
                    _remoteUserId, JsonSerializer.Serialize(candidate));
        };

        pc.onconnectionstatechange += state =>
        {
            _ = Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (state == RTCPeerConnectionState.connected)
                {
                    IsCallActive = true;
                    IsOutgoing   = false;
                    IsIncoming   = false;
                    StatusText   = $"In call with {RemoteName}";
                }
                else if (state == RTCPeerConnectionState.failed ||
                         state == RTCPeerConnectionState.closed)
                {
                    DoNavigateBack();
                }
            });
        };

        return pc;
    }

    // ── Outgoing ───────────────────────────────────────────────────────────────
    private async Task StartOutgoingCallAsync()
    {
        try
        {
            _pc = CreatePeerConnection();
            StartPortAudio();
            await StartCameraAsync();
            var offer = _pc.createOffer(null);
            await _pc.setLocalDescription(offer);
            await ServerClient.Instance.CallUserAsync(_remoteUserId, offer.sdp);
        }
        catch (Exception ex)
        {
            _ = Dispatcher.UIThread.InvokeAsync(() => StatusText = $"Error: {ex.Message}");
        }
    }

    // ── Accept incoming ────────────────────────────────────────────────────────
    [RelayCommand]
    async Task Accept()
    {
        try
        {
            StatusText = "Connecting…";
            _pc = CreatePeerConnection();
            StartPortAudio();
            await StartCameraAsync();

            _pc.setRemoteDescription(new RTCSessionDescriptionInit
            {
                type = RTCSdpType.offer,
                sdp  = _sdpOffer
            });

            var answer = _pc.createAnswer(null);
            await _pc.setLocalDescription(answer);
            await ServerClient.Instance.CallAnswerAsync(_remoteUserId, answer.sdp);

            IsCallActive = true;
            IsIncoming   = false;
            StatusText   = $"In call with {RemoteName}";
        }
        catch (Exception ex)
        {
            _ = Dispatcher.UIThread.InvokeAsync(() => StatusText = $"Error: {ex.Message}");
        }
    }

    [RelayCommand]
    async Task Decline()
    {
        await ServerClient.Instance.HangUpAsync(_remoteUserId);
        DoNavigateBack();
    }

    [RelayCommand]
    void ToggleMute()
    {
        IsMuted    = !IsMuted;
        IsNotMuted = !IsMuted;
    }

    [RelayCommand]
    void ToggleCamera()
    {
        if (_camera == null) return;
        IsCameraOn  = !IsCameraOn;
        IsCameraOff = !IsCameraOn;
        _ = IsCameraOn ? _camera.ResumeVideo() : _camera.PauseVideo();
    }

    [RelayCommand]
    async Task HangUp()
    {
        try { await ServerClient.Instance.HangUpAsync(_remoteUserId); } catch { }
        DoNavigateBack();
    }

    // ── Signalling callbacks ───────────────────────────────────────────────────
    private void OnCallAnswered(Guid answererId, string sdpAnswer)
    {
        if (answererId != _remoteUserId || _pc == null) return;
        try { _pc.setRemoteDescription(new RTCSessionDescriptionInit { type = RTCSdpType.answer, sdp = sdpAnswer }); }
        catch { }
    }

    private void OnRemoteIceCandidate(Guid fromId, string candidateJson)
    {
        if (fromId != _remoteUserId || _pc == null) return;
        try
        {
            var init = JsonSerializer.Deserialize<RTCIceCandidateInit>(candidateJson);
            if (init != null) _pc.addIceCandidate(init);
        }
        catch { }
    }

    private void OnCallEnded(Guid fromId)
    {
        if (fromId != _remoteUserId) return;
        _ = Dispatcher.UIThread.InvokeAsync(() => { StatusText = "Call ended"; DoNavigateBack(); });
    }

    // ── Camera ─────────────────────────────────────────────────────────────────
    private async Task StartCameraAsync()
    {
        try
        {
            FFmpegInit.Initialise(FfmpegLogLevelEnum.AV_LOG_FATAL, AppState.FfmpegLibPath);

            var cams = FFmpegCameraManager.GetCameraDevices();
            if (cams == null || cams.Count == 0) return;

            // Use saved preference or first available
            string pref = AppState.Instance.CameraDevicePath;
            var cam = cams.Find(c => c.Path == pref) ?? cams[0];

            _camera = new FFmpegCameraSource(cam.Path);
            _camera.SetVideoSourceFormat(new VideoFormat(VideoCodecsEnum.VP8, 96, 90000, null));

            // Local preview — raw BGR frames
            _camera.OnVideoSourceRawSample += (_, width, height, sample, pixFmt) =>
            {
                if (!IsCameraOn) return;
                RenderLocal(width, height, sample, pixFmt);
            };

            // Encoded VP8 → send via RTP
            _camera.OnVideoSourceEncodedSample += (durationRtp, sample) =>
            {
                if (!IsCameraOn || _pc == null) return;
                try { _pc.SendVideo((uint)durationRtp, sample); } catch { }
            };

            await _camera.StartVideo();
            IsCameraOn  = true;
            IsCameraOff = false;
        }
        catch
        {
            IsCameraOn  = false;
            IsCameraOff = true;
        }
    }

    // ── Remote video ───────────────────────────────────────────────────────────
    private void OnRemoteVideoDecoded(byte[] sample, uint width, uint height, int stride, VideoPixelFormatsEnum pixFmt)
    {
        if (width == 0 || height == 0) return;
        int w = (int)width, h = (int)height;
        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            try
            {
                HasRemoteVideo = true;
                var bmp = RemoteFrame;
                EnsureBitmap(ref bmp, w, h);
                WriteRawToBgra(bmp!, sample, w, h, pixFmt);
                RemoteFrame = bmp;
            }
            catch { }
        }, DispatcherPriority.Render);
    }

    private void RenderLocal(int w, int h, byte[] raw, VideoPixelFormatsEnum pixFmt)
    {
        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            try
            {
                var bmp = LocalFrame;
                EnsureBitmap(ref bmp, w, h);
                WriteRawToBgra(bmp!, raw, w, h, pixFmt);
                LocalFrame = bmp;
            }
            catch { }
        }, DispatcherPriority.Render);
    }

    private static WriteableBitmap EnsureBitmap(ref WriteableBitmap? bmp, int w, int h)
    {
        if (bmp == null || bmp.PixelSize.Width != w || bmp.PixelSize.Height != h)
            bmp = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96),
                                      PixelFormat.Bgra8888, AlphaFormat.Opaque);
        return bmp;
    }

    private static unsafe void WriteRawToBgra(WriteableBitmap bmp, byte[] raw, int w, int h, VideoPixelFormatsEnum pixFmt)
    {
        using var fb = bmp.Lock();
        var dst    = (byte*)fb.Address;
        int pixels = Math.Min(raw.Length / 3, w * h);

        if (pixFmt == VideoPixelFormatsEnum.Bgr || pixFmt == VideoPixelFormatsEnum.Bgra)
        {
            int stride = pixFmt == VideoPixelFormatsEnum.Bgra ? 4 : 3;
            for (int i = 0; i < pixels; i++)
            {
                dst[i*4+0] = raw[i*stride+0]; // B
                dst[i*4+1] = raw[i*stride+1]; // G
                dst[i*4+2] = raw[i*stride+2]; // R
                dst[i*4+3] = 255;
            }
        }
        else // RGB or unknown → swap R↔B into BGRA
        {
            for (int i = 0; i < pixels; i++)
            {
                dst[i*4+0] = raw[i*3+2]; // B←R
                dst[i*4+1] = raw[i*3+1]; // G
                dst[i*4+2] = raw[i*3+0]; // R←B
                dst[i*4+3] = 255;
            }
        }
    }

    // ── PortAudio ──────────────────────────────────────────────────────────────
    private void StartPortAudio()
    {
        try
        {
            PortAudio.Initialize();
            _paInitialised = true;

            int inDev  = ResolveDevice(AppState.Instance.MicDeviceIndex,     input: true);
            int outDev = ResolveDevice(AppState.Instance.SpeakerDeviceIndex, input: false);

            if (inDev >= 0)
            {
                var info  = PortAudio.GetDeviceInfo(inDev);
                var param = new StreamParameters
                {
                    device           = inDev,
                    channelCount     = OpusChannels,
                    sampleFormat     = SampleFormat.Int16,
                    suggestedLatency = info.defaultLowInputLatency
                };
                _captureStream = new Stream(param, null, OpusRate, OpusFrameSize,
                    StreamFlags.ClipOff, CaptureCallback, IntPtr.Zero);
                _captureStream.Start();
            }

            if (outDev >= 0)
            {
                var info  = PortAudio.GetDeviceInfo(outDev);
                var param = new StreamParameters
                {
                    device           = outDev,
                    channelCount     = OpusChannels,
                    sampleFormat     = SampleFormat.Int16,
                    suggestedLatency = info.defaultLowOutputLatency
                };
                _playbackStream = new Stream(null, param, OpusRate, OpusFrameSize,
                    StreamFlags.ClipOff, PlaybackCallback, IntPtr.Zero);
                _playbackStream.Start();
            }
        }
        catch (Exception ex)
        {
            _ = Dispatcher.UIThread.InvokeAsync(() => StatusText = $"Audio error: {ex.Message}");
        }
    }

    private static int ResolveDevice(int preference, bool input)
    {
        if (preference >= 0 && preference < PortAudio.DeviceCount)
        {
            var info = PortAudio.GetDeviceInfo(preference);
            if (input ? info.maxInputChannels > 0 : info.maxOutputChannels > 0)
                return preference;
        }
        return input ? PortAudio.DefaultInputDevice : PortAudio.DefaultOutputDevice;
    }

    private StreamCallbackResult CaptureCallback(
        nint input, nint output, uint frameCount,
        ref StreamCallbackTimeInfo timeInfo, StreamCallbackFlags flags, nint userData)
    {
        if (input == IntPtr.Zero || IsMuted) return StreamCallbackResult.Continue;
        unsafe
        {
            var src = new Span<short>((void*)input, (int)frameCount);
            for (int i = 0; i < src.Length; i++)
            {
                _capBuf[_capWrite % _capBuf.Length] = src[i];
                _capWrite++;
            }
        }
        var encodeBuf = new byte[4000];
        while (_capWrite - _capRead >= OpusFrameSize)
        {
            var frame = new short[OpusFrameSize];
            for (int i = 0; i < OpusFrameSize; i++)
                frame[i] = _capBuf[_capRead++ % _capBuf.Length];
            try
            {
                int len = _opusEnc.Encode(frame.AsSpan(), OpusFrameSize, encodeBuf.AsSpan(), encodeBuf.Length);
                if (len > 0)
                {
                    var pkt = new byte[len];
                    encodeBuf.AsSpan(0, len).CopyTo(pkt);
                    _pc?.SendAudio((uint)OpusFrameSize, pkt);
                }
            }
            catch { }
        }
        return StreamCallbackResult.Continue;
    }

    private StreamCallbackResult PlaybackCallback(
        nint input, nint output, uint frameCount,
        ref StreamCallbackTimeInfo timeInfo, StreamCallbackFlags flags, nint userData)
    {
        if (output == IntPtr.Zero) return StreamCallbackResult.Continue;
        unsafe
        {
            var dst = new Span<short>((void*)output, (int)frameCount);
            int r   = _playRead;
            for (int i = 0; i < dst.Length; i++)
                dst[i] = r < _playWrite ? _playBuf[r++ % _playBuf.Length] : (short)0;
            _playRead = r;
        }
        return StreamCallbackResult.Continue;
    }

    // ── Cleanup ────────────────────────────────────────────────────────────────
    private void Cleanup()
    {
        if (_cleanedUp) return;
        _cleanedUp = true;
        UnsubscribeSignalling();
        try { _camera?.CloseVideo().Wait(500); } catch { }
        try { _videoSink?.CloseVideoSink().Wait(500); } catch { }
        try { _captureStream?.Stop();  _captureStream?.Dispose();  } catch { }
        try { _playbackStream?.Stop(); _playbackStream?.Dispose(); } catch { }
        if (_paInitialised) { try { PortAudio.Terminate(); } catch { } }
        try { _pc?.close(); _pc?.Dispose(); } catch { }
        _camera         = null;
        _videoSink      = null;
        _captureStream  = null;
        _playbackStream = null;
        _pc             = null;
    }

    private void DoNavigateBack()
    {
        Cleanup();
        _ = Dispatcher.UIThread.InvokeAsync(() => _main.Navigate(new DMListViewModel(_main)));
    }
}
