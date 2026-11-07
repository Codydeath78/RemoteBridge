using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using RemoteBridge.Desktop.Models;
using RemoteBridge.Desktop.Services;
using RemoteBridge.Desktop.Utilities;
using RemoteBridge.Protocol;
using RemoteBridge.WindowsMedia;


namespace RemoteBridge.Desktop.ViewModels;


public partial class MainWindowViewModel :
    ViewModelBase
{
    private const string
        StreamStoppedControlMessage =
            "__RB_CONTROL_STREAM_STOPPED_V1__";


    private readonly RelayClient
        _relayClient;


    private readonly WindowsInputService
        _windowsInputService;


#if WINDOWS

    private H264RelayStreamSession?
        _h264RelayStreamSession;

#endif


    private readonly Stopwatch
        _mouseSendTimer =
            Stopwatch.StartNew();


    private readonly object
        _mousePositionSync =
            new();


    private readonly object
        _hostRemoteKeysSync =
            new();


    private readonly HashSet<RemoteKey>
        _hostRemoteKeysDown =
            new();


    private readonly object
        _viewerRemoteKeysSync =
            new();


    private readonly HashSet<RemoteKey>
        _viewerRemoteKeysDown =
            new();


    private bool
        _remoteLeftButtonDown;


    private long
        _mousePositionVersion;


    private double
        _pendingMouseX;


    private double
        _pendingMouseY;


    private int
        _mouseSendScheduled;


    public Func<
        byte[],
        long,
        ushort,
        ushort,
        CancellationToken,
        ValueTask
    >?
        H264VideoSubmitter
    {
        get;
        set;
    }


    public MainWindowViewModel()
        : this(
            new RelayClient(),
            new WindowsInputService()
        )
    {
    }


    public MainWindowViewModel(
        RelayClient relayClient)
        : this(
            relayClient,
            new WindowsInputService()
        )
    {
    }


    private MainWindowViewModel(
        RelayClient relayClient,
        WindowsInputService windowsInputService)
    {
        _relayClient =
            relayClient;


        _windowsInputService =
            windowsInputService;


        _relayClient.ViewerRequested +=
            OnViewerRequested;


        _relayClient.SessionApproved +=
            OnSessionApproved;


        _relayClient.SessionRejected +=
            OnSessionRejected;


        _relayClient.ErrorReceived +=
            OnRelayError;


        _relayClient.SessionDataReceived +=
            OnSessionDataReceived;


        _relayClient.ScreenFrameReceived +=
            OnScreenFrameReceived;


        _relayClient.PeerDisconnected +=
            OnPeerDisconnected;


        _relayClient.RemoteMouseMoveReceived +=
            OnRemoteMouseMoveReceived;


        _relayClient.RemoteMouseLeftDownReceived +=
            OnRemoteMouseLeftDownReceived;


        _relayClient.RemoteMouseLeftUpReceived +=
            OnRemoteMouseLeftUpReceived;


        _relayClient.SessionEnded +=
            OnSessionEnded;


        _relayClient.RemoteKeyDownReceived +=
            OnRemoteKeyDownReceived;


        _relayClient.RemoteKeyUpReceived +=
            OnRemoteKeyUpReceived;


        _relayClient.RemoteMouseWheelReceived +=
            OnRemoteMouseWheelReceived;
    }


    public IReadOnlyList<
        StreamQualityProfile
    > StreamQualityOptions =>
        StreamQualityProfiles.All;


    [ObservableProperty]
    private StreamQualityProfile
        _selectedStreamQuality =
            StreamQualityProfiles
                .HighQuality60;


    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(CanControlRemoteHost)
    )]
    [NotifyPropertyChangedFor(
        nameof(RemoteControlHint)
    )]
    private bool
        _isRemoteStreamActive;


    [ObservableProperty]
    private string
        _sessionCode =
            string.Empty;


    [ObservableProperty]
    private string
        _hostSessionCode =
            string.Empty;


    [ObservableProperty]
    private string
        _statusMessage =
            string.Empty;


    [ObservableProperty]
    private RemoteRole
        _role =
            RemoteRole.None;


    [ObservableProperty]
    private RemoteSessionState
        _sessionState =
            RemoteSessionState.Idle;


    [ObservableProperty]
    private bool
        _hasIncomingRequest;


    [ObservableProperty]
    private string
        _incomingViewerDeviceName =
            string.Empty;


    //
    // Kept as lightweight state for diagnostics and
    // code-behind failure reporting. It is no longer
    // used to render a legacy JPEG preview.
    //
    [ObservableProperty]
    private string
        _remoteScreenInfo =
            string.Empty;


    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(CanStartLiveScreenSharing)
    )]
    [NotifyPropertyChangedFor(
        nameof(CanStopLiveScreenSharing)
    )]
    private bool
        _isLiveScreenSharing;


    public bool HasStatusMessage =>
        !string.IsNullOrWhiteSpace(
            StatusMessage
        );


    public bool HasHostSessionCode =>
        !string.IsNullOrWhiteSpace(
            HostSessionCode
        );


    public bool IsWindowsPlatform =>
        OperatingSystem.IsWindows();


    public bool IsHosting =>
        Role ==
        RemoteRole.Host;


    public bool IsViewer =>
        Role ==
        RemoteRole.Viewer;


    public bool IsConnected =>
        SessionState ==
        RemoteSessionState.Connected;


    public bool IsViewerConnected =>
        IsViewer &&
        IsConnected;


    public bool CanStartLiveScreenSharing =>
        IsWindowsPlatform &&
        IsHosting &&
        IsConnected &&
        !IsLiveScreenSharing;


    public bool CanStopLiveScreenSharing =>
        IsHosting &&
        IsConnected &&
        IsLiveScreenSharing;


    public bool CanEndRemoteSession =>
        IsHosting &&
        IsConnected;


    public bool CanControlRemoteHost =>
        IsViewerConnected &&
        IsRemoteStreamActive;


    public string RemoteControlHint =>
        IsRemoteStreamActive
            ? "Click the remote screen to focus it. Mouse, scrolling, and keyboard input are enabled."
            : "Live sharing is paused. Remote mouse and keyboard control are disabled.";


    partial void OnRoleChanged(
        RemoteRole value)
    {
        NotifyDerivedSessionState();
    }


    partial void OnSessionStateChanged(
        RemoteSessionState value)
    {
        NotifyDerivedSessionState();
    }


    partial void OnStatusMessageChanged(
        string value)
    {
        OnPropertyChanged(
            nameof(
                HasStatusMessage
            )
        );
    }


    partial void OnHostSessionCodeChanged(
        string value)
    {
        OnPropertyChanged(
            nameof(
                HasHostSessionCode
            )
        );
    }


    private void NotifyDerivedSessionState()
    {
        OnPropertyChanged(
            nameof(
                IsHosting
            )
        );


        OnPropertyChanged(
            nameof(
                IsViewer
            )
        );


        OnPropertyChanged(
            nameof(
                IsConnected
            )
        );


        OnPropertyChanged(
            nameof(
                IsViewerConnected
            )
        );


        OnPropertyChanged(
            nameof(
                CanStartLiveScreenSharing
            )
        );


        OnPropertyChanged(
            nameof(
                CanStopLiveScreenSharing
            )
        );


        OnPropertyChanged(
            nameof(
                CanEndRemoteSession
            )
        );


        OnPropertyChanged(
            nameof(
                CanControlRemoteHost
            )
        );


        OnPropertyChanged(
            nameof(
                RemoteControlHint
            )
        );
    }


    [RelayCommand]
    private async Task ShareComputerAsync()
    {
        try
        {
            Role =
                RemoteRole.Host;


            SessionState =
                RemoteSessionState.Idle;


            StatusMessage =
                "Connecting to RemoteBridge...";


            HostSessionCode =
                string.Empty;


            string code =
                await _relayClient
                    .RegisterHostAsync();


            HostSessionCode =
                SessionCodeUtility
                    .Format(
                        code
                    );


            Role =
                RemoteRole.Host;


            SessionState =
                RemoteSessionState.Hosting;


            StatusMessage =
                "Waiting for another device to connect.";
        }
        catch (Exception ex)
        {
            Role =
                RemoteRole.None;


            SessionState =
                RemoteSessionState.Idle;


            HostSessionCode =
                string.Empty;


            StatusMessage =
                "Unable to start sharing: " +
                ex.Message;
        }
    }


    [RelayCommand]
    private async Task ConnectAsync()
    {
        string normalizedCode =
            SessionCodeUtility
                .Normalize(
                    SessionCode
                );


        if (
            normalizedCode.Length !=
            9
        )
        {
            StatusMessage =
                "Please enter a valid 9-digit RemoteBridge session code.";


            return;
        }


        try
        {
            Role =
                RemoteRole.Viewer;


            SessionState =
                RemoteSessionState.Joining;


            StatusMessage =
                "Connecting to RemoteBridge...";


            await _relayClient
                .JoinSessionAsync(
                    normalizedCode
                );


            SessionState =
                RemoteSessionState.AwaitingApproval;


            StatusMessage =
                "Connection request sent. Waiting for host approval.";
        }
        catch (Exception ex)
        {
            Role =
                RemoteRole.None;


            SessionState =
                RemoteSessionState.Idle;


            StatusMessage =
                "Unable to connect: " +
                ex.Message;
        }
    }


    [RelayCommand]
    private async Task ApproveViewerAsync()
    {
        try
        {
            await _relayClient
                .ApproveViewerAsync();


            HasIncomingRequest =
                false;


            StatusMessage =
                "Viewer approved. Remote session connected.";
        }
        catch (Exception ex)
        {
            StatusMessage =
                "Unable to approve viewer: " +
                ex.Message;
        }
    }


    [RelayCommand]
    private async Task RejectViewerAsync()
    {
        try
        {
            await _relayClient
                .RejectViewerAsync();


            HasIncomingRequest =
                false;


            IncomingViewerDeviceName =
                string.Empty;


            StatusMessage =
                "Connection request rejected.";
        }
        catch (Exception ex)
        {
            StatusMessage =
                "Unable to reject viewer: " +
                ex.Message;
        }
    }


    [RelayCommand]
private async Task StartLiveScreenSharingAsync()
{
    if (
        !CanStartLiveScreenSharing
    )
    {
        StatusMessage =
            "Live screen sharing is not currently available.";


        return;
    }


#if WINDOWS

    if (
        _h264RelayStreamSession
        is not null
    )
    {
        return;
    }


    H264RelayStreamSession?
        session =
            null;


    try
    {
        StreamQualityProfile profile =
            SelectedStreamQuality;


        H264StreamSettings settings =
            new(
                Width:
                    checked(
                        (uint)profile.MaxWidth
                    ),

                Height:
                    checked(
                        (uint)profile.MaxHeight
                    ),

                FramesPerSecond:
                    checked(
                        (uint)profile.TargetFps
                    ),

                Bitrate:
                    profile.H264Bitrate
            );


        settings.Validate();


        session =
            new H264RelayStreamSession(
                _relayClient,
                settings
            );


        session.Faulted +=
            OnHostStreamFaulted;


        _h264RelayStreamSession =
            session;


        session.Start();


        IsLiveScreenSharing =
            true;


        StatusMessage =
            "Live screen sharing started.";
    }
    catch (Exception ex)
    {
        if (
            ReferenceEquals(
                _h264RelayStreamSession,
                session
            )
        )
        {
            _h264RelayStreamSession =
                null;
        }


        if (session is not null)
        {
            session.Faulted -=
                OnHostStreamFaulted;


            try
            {
                await session
                    .DisposeAsync();
            }
            catch
            {
                // Best-effort startup cleanup.
            }
        }


        IsLiveScreenSharing =
            false;


        StatusMessage =
            "Unable to start live screen sharing: " +
            ex.Message;
    }

#else

    await Task.CompletedTask;


    StatusMessage =
        "Live screen sharing is currently available only on Windows.";

#endif
}


    [RelayCommand]
    private async Task StopLiveScreenSharingAsync()
    {
        if (!IsHosting)
        {
            return;
        }


        //
        // Revoke Host-side control before media teardown.
        //
        IsLiveScreenSharing =
            false;


        ReleaseHostRemoteInputState();


        await DisposeHostStreamAsync();


        //
        // Notify the Viewer only after media has stopped,
        // so a late video frame cannot reactivate control.
        //
        await NotifyViewerStreamStoppedAsync();


        StatusMessage =
            "Live screen sharing stopped.";
    }


    [RelayCommand]
    private async Task EndRemoteSessionAsync()
    {
        if (
            !IsHosting ||
            !IsConnected
        )
        {
            return;
        }


        try
        {
            IsLiveScreenSharing =
                false;


            ReleaseHostRemoteInputState();


            await DisposeHostStreamAsync();


            await _relayClient
                .EndSessionAsync();


            SessionState =
                RemoteSessionState.Hosting;


            StatusMessage =
                "Remote session ended. Waiting for another device to connect.";
        }
        catch (Exception ex)
        {
            StatusMessage =
                "Unable to end remote session: " +
                ex.Message;
        }
    }


#if WINDOWS

    private void OnHostStreamFaulted(
        Exception exception)
    {
        Dispatcher.UIThread.Post(
            () =>
            {
                IsLiveScreenSharing =
                    false;


                ReleaseHostRemoteInputState();


                StatusMessage =
                    "Live screen sharing stopped unexpectedly: " +
                    exception.Message;
            }
        );


        _ =
            HandleHostStreamFaultAsync();
    }


    private async Task HandleHostStreamFaultAsync()
    {
        //
        // Let the Faulted callback return before disposing
        // the session that raised it.
        //
        await Task.Yield();


        await DisposeHostStreamAsync();


        await NotifyViewerStreamStoppedAsync();
    }

#endif


    private async Task DisposeHostStreamAsync()
    {
#if WINDOWS

        H264RelayStreamSession?
            session =
                Interlocked.Exchange(
                    ref _h264RelayStreamSession,
                    null
                );


        if (session is null)
        {
            return;
        }


        session.Faulted -=
            OnHostStreamFaulted;


        try
        {
            await session
                .DisposeAsync();
        }
        catch
        {
            //
            // Stream shutdown is best-effort.
            // Session/control cleanup must continue.
            //
        }

#else

        await Task.CompletedTask;

#endif
    }


    private async Task NotifyViewerStreamStoppedAsync()
    {
        if (
            !IsHosting ||
            !IsConnected
        )
        {
            return;
        }


        try
        {
            await _relayClient
                .SendSessionDataAsync(
                    StreamStoppedControlMessage
                );
        }
        catch
        {
            //
            // Shutdown must still complete if the
            // Viewer disappears during notification.
            //
        }
    }


    private void OnViewerRequested(
        string deviceName)
    {
        Dispatcher.UIThread.Post(
            () =>
            {
                IncomingViewerDeviceName =
                    deviceName;


                HasIncomingRequest =
                    true;


                StatusMessage =
                    $"{deviceName} wants to connect.";
            }
        );
    }


    private void OnSessionApproved()
    {
        Dispatcher.UIThread.Post(
            () =>
            {
                SessionState =
                    RemoteSessionState.Connected;


                HasIncomingRequest =
                    false;


                StatusMessage =
                    "Remote session connected.";
            }
        );
    }


    private void OnSessionRejected(
        string message)
    {
        ReleaseHostRemoteInputState();


        ClearViewerRemoteKeysLocal();


        _ =
            DisposeHostStreamAsync();


        Dispatcher.UIThread.Post(
            () =>
            {
                HasIncomingRequest =
                    false;


                IsLiveScreenSharing =
                    false;


                IsRemoteStreamActive =
                    false;


                if (Role == RemoteRole.Host)
                {
                    SessionState =
                        RemoteSessionState.Hosting;
                }
                else
                {
                    Role =
                        RemoteRole.None;


                    SessionState =
                        RemoteSessionState.Idle;
                }


                StatusMessage =
                    message;
            }
        );
    }


    private void OnSessionEnded()
    {
        ReleaseHostRemoteInputState();


        ClearViewerRemoteKeysLocal();


        _ =
            DisposeHostStreamAsync();


        Dispatcher.UIThread.Post(
            () =>
            {
                IsLiveScreenSharing =
                    false;


                IsRemoteStreamActive =
                    false;


                if (Role == RemoteRole.Host)
                {
                    SessionState =
                        RemoteSessionState.Hosting;


                    StatusMessage =
                        "Remote session ended. " +
                        "Waiting for another device to connect.";
                }
                else
                {
                    Role =
                        RemoteRole.None;


                    SessionState =
                        RemoteSessionState.Idle;


                    StatusMessage =
                        "The remote session ended.";
                }
            }
        );
    }


    private void OnPeerDisconnected(
        string message)
    {
        ReleaseHostRemoteInputState();


        ClearViewerRemoteKeysLocal();


        _ =
            DisposeHostStreamAsync();


        Dispatcher.UIThread.Post(
            () =>
            {
                HasIncomingRequest =
                    false;


                IsLiveScreenSharing =
                    false;


                IsRemoteStreamActive =
                    false;


                if (Role == RemoteRole.Host)
                {
                    SessionState =
                        RemoteSessionState.Hosting;
                }
                else
                {
                    Role =
                        RemoteRole.None;


                    SessionState =
                        RemoteSessionState.Idle;
                }


                StatusMessage =
                    message;
            }
        );
    }


    private void OnRelayError(
        string message)
    {
        Dispatcher.UIThread.Post(
            () =>
            {
                StatusMessage =
                    "Relay error: " +
                    message;
            }
        );
    }


    private void OnSessionDataReceived(
        string role,
        string deviceName,
        string data)
    {
        _ =
            deviceName;


        if (
            !string.Equals(
                role,
                "Host",
                StringComparison.OrdinalIgnoreCase
            ) ||
            !string.Equals(
                data,
                StreamStoppedControlMessage,
                StringComparison.Ordinal
            )
        )
        {
            return;
        }


        Dispatcher.UIThread.Post(
            () =>
            {
                IsRemoteStreamActive =
                    false;


                RemoteScreenInfo =
                    "Host stopped live sharing. " +
                    "Remote control is disabled.";


                StatusMessage =
                    "The host stopped live screen sharing.";


                _ =
                    ReleaseViewerRemoteKeysAsync();
            }
        );
    }


    private void OnScreenFrameReceived(
        byte[] payload)
    {
        if (
            !H264VideoPacket.TryParse(
                payload,
                out H264VideoPacketData?
                    video
            ) ||
            video is null
        )
        {
            //
            // Legacy JPEG frames and transport probes are
            // intentionally ignored by the production Viewer.
            //
            return;
        }


        Func<
            byte[],
            long,
            ushort,
            ushort,
            CancellationToken,
            ValueTask
        >?
            submitter =
                H264VideoSubmitter;


        if (submitter is null)
        {
            Dispatcher.UIThread.Post(
                () =>
                {
                    StatusMessage =
                        "Remote video arrived before the viewer surface was ready.";
                }
            );


            return;
        }


        try
        {
            //
            // Keep H.264 access units ordered.
            //
            submitter(
                    video.Bytes,
                    video.Timestamp100Ns,
                    video.Width,
                    video.Height,
                    CancellationToken.None
                )
                .AsTask()
                .GetAwaiter()
                .GetResult();


            //
            // Do not post a UI update for every 60 FPS frame.
            // Only activate the Viewer state when a stream
            // actually transitions from stopped to running.
            //
            if (!IsRemoteStreamActive)
            {
                Dispatcher.UIThread.Post(
                    () =>
                    {
                        if (IsRemoteStreamActive)
                        {
                            return;
                        }


                        IsRemoteStreamActive =
                            true;


                        RemoteScreenInfo =
                            $"Live remote desktop • " +
                            $"{video.Width}×{video.Height}";
                    }
                );
            }
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(
                () =>
                {
                    IsRemoteStreamActive =
                        false;


                    StatusMessage =
                        "Remote video could not be displayed: " +
                        ex.Message;


                    _ =
                        ReleaseViewerRemoteKeysAsync();
                }
            );
        }
    }


    private void OnRemoteMouseMoveReceived(
        double x,
        double y)
    {
        if (
            !OperatingSystem.IsWindows() ||
            !IsHosting ||
            !IsConnected ||
            !IsLiveScreenSharing
        )
        {
            return;
        }


        try
        {
            _windowsInputService
                .MoveMouseNormalized(
                    x,
                    y
                );
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(
                () =>
                {
                    StatusMessage =
                        "Remote mouse error: " +
                        ex.Message;
                }
            );
        }
    }


    private void OnRemoteMouseLeftDownReceived()
    {
        if (
            !OperatingSystem.IsWindows() ||
            !IsHosting ||
            !IsConnected ||
            !IsLiveScreenSharing
        )
        {
            return;
        }


        try
        {
            _windowsInputService
                .LeftButtonDown();


            _remoteLeftButtonDown =
                true;
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(
                () =>
                {
                    StatusMessage =
                        "Remote click error: " +
                        ex.Message;
                }
            );
        }
    }


    private void OnRemoteMouseLeftUpReceived()
    {
        if (
            !OperatingSystem.IsWindows() ||
            !IsHosting ||
            !IsConnected ||
            !IsLiveScreenSharing
        )
        {
            return;
        }


        try
        {
            _windowsInputService
                .LeftButtonUp();


            _remoteLeftButtonDown =
                false;
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(
                () =>
                {
                    StatusMessage =
                        "Remote click error: " +
                        ex.Message;
                }
            );
        }
    }


    private void OnRemoteMouseWheelReceived(
        int wheelDelta)
    {
        if (
            !OperatingSystem.IsWindows() ||
            !IsHosting ||
            !IsConnected ||
            !IsLiveScreenSharing
        )
        {
            return;
        }


        try
        {
            _windowsInputService
                .MouseWheel(
                    wheelDelta
                );
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(
                () =>
                {
                    StatusMessage =
                        "Remote scroll error: " +
                        ex.Message;
                }
            );
        }
    }


    private void OnRemoteKeyDownReceived(
        RemoteKey key)
    {
        if (
            !OperatingSystem.IsWindows() ||
            !IsHosting ||
            !IsConnected ||
            !IsLiveScreenSharing
        )
        {
            return;
        }


        try
        {
            _windowsInputService
                .KeyDown(
                    key
                );


            lock (_hostRemoteKeysSync)
            {
                _hostRemoteKeysDown
                    .Add(
                        key
                    );
            }
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(
                () =>
                {
                    StatusMessage =
                        "Remote keyboard error: " +
                        ex.Message;
                }
            );
        }
    }


    private void OnRemoteKeyUpReceived(
        RemoteKey key)
    {
        if (
            !OperatingSystem.IsWindows() ||
            !IsHosting ||
            !IsConnected ||
            !IsLiveScreenSharing
        )
        {
            return;
        }


        try
        {
            _windowsInputService
                .KeyUp(
                    key
                );


            lock (_hostRemoteKeysSync)
            {
                _hostRemoteKeysDown
                    .Remove(
                        key
                    );
            }
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(
                () =>
                {
                    StatusMessage =
                        "Remote keyboard error: " +
                        ex.Message;
                }
            );
        }
    }


    private void ReleaseHostRemoteInputState()
    {
        if (
            !OperatingSystem.IsWindows() ||
            !IsHosting
        )
        {
            return;
        }


        RemoteKey[] keys;


        lock (_hostRemoteKeysSync)
        {
            keys =
                _hostRemoteKeysDown
                    .ToArray();


            _hostRemoteKeysDown
                .Clear();
        }


        foreach (
            RemoteKey key
            in keys
        )
        {
            try
            {
                _windowsInputService
                    .KeyUp(
                        key
                    );
            }
            catch
            {
                // Best-effort cleanup.
            }
        }


        if (!_remoteLeftButtonDown)
        {
            return;
        }


        try
        {
            _windowsInputService
                .LeftButtonUp();
        }
        catch
        {
            // Best-effort cleanup.
        }


        _remoteLeftButtonDown =
            false;
    }


    public void QueueRemoteMouseMove(
        double normalizedX,
        double normalizedY)
    {
        if (!CanControlRemoteHost)
        {
            return;
        }


        lock (_mousePositionSync)
        {
            _pendingMouseX =
                Math.Clamp(
                    normalizedX,
                    0.0,
                    1.0
                );


            _pendingMouseY =
                Math.Clamp(
                    normalizedY,
                    0.0,
                    1.0
                );


            _mousePositionVersion++;
        }


        if (
            Interlocked.Exchange(
                ref _mouseSendScheduled,
                1
            ) != 0
        )
        {
            return;
        }


        _ =
            SendLatestMousePositionAsync();
    }


    private async Task SendLatestMousePositionAsync()
    {
        long sentVersion =
            -1;


        try
        {
            if (!CanControlRemoteHost)
            {
                return;
            }


            const int minimumIntervalMs =
                8;


            int elapsed =
                (int)_mouseSendTimer
                    .ElapsedMilliseconds;


            if (
                elapsed <
                minimumIntervalMs
            )
            {
                await Task.Delay(
                    minimumIntervalMs -
                    elapsed
                );
            }


            if (!CanControlRemoteHost)
            {
                return;
            }


            double x;
            double y;


            lock (_mousePositionSync)
            {
                x =
                    _pendingMouseX;


                y =
                    _pendingMouseY;


                sentVersion =
                    _mousePositionVersion;
            }


            if (!CanControlRemoteHost)
            {
                return;
            }


            _mouseSendTimer.Restart();


            await _relayClient
                .SendMouseMoveAsync(
                    x,
                    y
                );
        }
        catch (Exception ex)
        {
            if (CanControlRemoteHost)
            {
                Dispatcher.UIThread.Post(
                    () =>
                    {
                        StatusMessage =
                            "Unable to send mouse movement: " +
                            ex.Message;
                    }
                );
            }
        }
        finally
        {
            Interlocked.Exchange(
                ref _mouseSendScheduled,
                0
            );


            if (CanControlRemoteHost)
            {
                long latestVersion;


                lock (_mousePositionSync)
                {
                    latestVersion =
                        _mousePositionVersion;
                }


                if (
                    latestVersion !=
                    sentVersion &&
                    Interlocked.Exchange(
                        ref _mouseSendScheduled,
                        1
                    ) == 0
                )
                {
                    _ =
                        SendLatestMousePositionAsync();
                }
            }
        }
    }


    public async Task
        SendRemoteMouseMoveImmediateAsync(
            double x,
            double y)
    {
        if (!CanControlRemoteHost)
        {
            return;
        }


        await _relayClient
            .SendMouseMoveAsync(
                Math.Clamp(
                    x,
                    0.0,
                    1.0
                ),
                Math.Clamp(
                    y,
                    0.0,
                    1.0
                )
            );
    }


    public async Task SendRemoteLeftButtonDownAsync()
    {
        if (!CanControlRemoteHost)
        {
            return;
        }


        try
        {
            await _relayClient
                .SendMouseLeftDownAsync();
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(
                () =>
                {
                    StatusMessage =
                        "Unable to send click: " +
                        ex.Message;
                }
            );
        }
    }


    public async Task SendRemoteLeftButtonUpAsync()
    {
        if (!CanControlRemoteHost)
        {
            return;
        }


        try
        {
            await _relayClient
                .SendMouseLeftUpAsync();
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(
                () =>
                {
                    StatusMessage =
                        "Unable to send click: " +
                        ex.Message;
                }
            );
        }
    }


    public async Task SendRemoteMouseWheelAsync(
        int wheelDelta)
    {
        if (
            !CanControlRemoteHost ||
            wheelDelta == 0
        )
        {
            return;
        }


        try
        {
            await _relayClient
                .SendMouseWheelAsync(
                    wheelDelta
                );
        }
        catch (Exception ex)
        {
            StatusMessage =
                "Unable to send mouse scroll: " +
                ex.Message;
        }
    }


    public async Task SendRemoteKeyDownAsync(
        RemoteKey key)
    {
        if (!CanControlRemoteHost)
        {
            return;
        }


        lock (_viewerRemoteKeysSync)
        {
            _viewerRemoteKeysDown
                .Add(
                    key
                );
        }


        try
        {
            await _relayClient
                .SendKeyDownAsync(
                    key
                );
        }
        catch (Exception ex)
        {
            StatusMessage =
                "Unable to send keyboard input: " +
                ex.Message;
        }
    }


    public async Task SendRemoteKeyUpAsync(
        RemoteKey key)
    {
        if (!CanControlRemoteHost)
        {
            lock (_viewerRemoteKeysSync)
            {
                _viewerRemoteKeysDown
                    .Remove(
                        key
                    );
            }


            return;
        }


        lock (_viewerRemoteKeysSync)
        {
            _viewerRemoteKeysDown
                .Remove(
                    key
                );
        }


        try
        {
            await _relayClient
                .SendKeyUpAsync(
                    key
                );
        }
        catch (Exception ex)
        {
            StatusMessage =
                "Unable to send keyboard input: " +
                ex.Message;
        }
    }


    public async Task ReleaseViewerRemoteKeysAsync()
    {
        if (!IsViewerConnected)
        {
            ClearViewerRemoteKeysLocal();


            return;
        }


        RemoteKey[] keys;


        lock (_viewerRemoteKeysSync)
        {
            keys =
                _viewerRemoteKeysDown
                    .ToArray();


            _viewerRemoteKeysDown
                .Clear();
        }


        foreach (
            RemoteKey key
            in keys
        )
        {
            try
            {
                await _relayClient
                    .SendKeyUpAsync(
                        key
                    );
            }
            catch
            {
                //
                // Host-side cleanup is the final fallback.
                //
            }
        }
    }


    private void ClearViewerRemoteKeysLocal()
    {
        lock (_viewerRemoteKeysSync)
        {
            _viewerRemoteKeysDown
                .Clear();
        }
    }


    public async Task ShutdownAsync()
    {
        IsLiveScreenSharing =
            false;


        ReleaseHostRemoteInputState();


        await DisposeHostStreamAsync();


        await ReleaseViewerRemoteKeysAsync();


        H264VideoSubmitter =
            null;


        _relayClient.ViewerRequested -=
            OnViewerRequested;


        _relayClient.SessionApproved -=
            OnSessionApproved;


        _relayClient.SessionRejected -=
            OnSessionRejected;


        _relayClient.ErrorReceived -=
            OnRelayError;


        _relayClient.SessionDataReceived -=
            OnSessionDataReceived;


        _relayClient.ScreenFrameReceived -=
            OnScreenFrameReceived;


        _relayClient.PeerDisconnected -=
            OnPeerDisconnected;


        _relayClient.RemoteMouseMoveReceived -=
            OnRemoteMouseMoveReceived;


        _relayClient.RemoteMouseLeftDownReceived -=
            OnRemoteMouseLeftDownReceived;


        _relayClient.RemoteMouseLeftUpReceived -=
            OnRemoteMouseLeftUpReceived;


        _relayClient.SessionEnded -=
            OnSessionEnded;


        _relayClient.RemoteKeyDownReceived -=
            OnRemoteKeyDownReceived;


        _relayClient.RemoteKeyUpReceived -=
            OnRemoteKeyUpReceived;


        _relayClient.RemoteMouseWheelReceived -=
            OnRemoteMouseWheelReceived;


        await _relayClient
            .DisconnectAsync();
    }
}