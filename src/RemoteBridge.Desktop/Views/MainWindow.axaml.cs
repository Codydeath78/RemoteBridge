using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

using RemoteBridge.Desktop.Utilities;
using RemoteBridge.Desktop.ViewModels;
using RemoteBridge.Protocol;


namespace RemoteBridge.Desktop.Views;


public partial class MainWindow :
    Window
{
#if WINDOWS

    private RemoteVideoSurface?
        _remoteH264VideoSurface;


    private MainWindowViewModel?
        _remoteH264ViewModel;

#endif


    private bool
        _isRemoteFullscreen;


    private WindowState
        _windowStateBeforeRemoteFullscreen;


    private double
        _mainContentWidthBeforeFullscreen;


    private Avalonia.Thickness
        _mainContentMarginBeforeFullscreen;


    private double
        _remoteInputHeightBeforeFullscreen;


    private double
        _remoteInputMinHeightBeforeFullscreen;


    private Avalonia.Vector
        _scrollOffsetBeforeFullscreen;


    private Avalonia.Thickness
        _remoteScreenPaddingBeforeFullscreen;


    private Avalonia.Thickness
        _remoteScreenBorderBeforeFullscreen;


    private Avalonia.CornerRadius
        _remoteScreenCornerRadiusBeforeFullscreen;


    public MainWindow()
    {
        InitializeComponent();
    }


    private void MainWindow_OnOpened(
        object? sender,
        EventArgs e)
    {
#if WINDOWS

        if (
            DataContext is not
                MainWindowViewModel
                    viewModel
        )
        {
            return;
        }


        RemoteVideoSurface?
            surface =
                this.FindControl<
                    RemoteVideoSurface
                >(
                    "RemoteH264VideoSurface"
                );


        if (surface is null)
        {
            return;
        }


        _remoteH264VideoSurface =
            surface;


        _remoteH264ViewModel =
            viewModel;


        viewModel.H264VideoSubmitter =
            (
                bytes,
                timestamp100Ns,
                width,
                height,
                cancellationToken
            ) =>
            {
                surface.SetVideoSize(
                    width,
                    height
                );


                return surface
                    .SubmitOwnedH264Async(
                        bytes,
                        timestamp100Ns,
                        cancellationToken
                    );
            };


        surface.PointerMovedNormalized +=
            RemoteH264VideoSurface_OnPointerMovedNormalized;


        surface.LeftButtonPressed +=
            RemoteH264VideoSurface_OnLeftButtonPressed;


        surface.LeftButtonReleased +=
            RemoteH264VideoSurface_OnLeftButtonReleased;


        surface.MouseWheel +=
            RemoteH264VideoSurface_OnMouseWheel;


        surface.PipelineFaulted +=
            RemoteH264VideoSurface_OnPipelineFaulted;


        viewModel.PropertyChanged +=
            RemoteH264ViewModel_OnPropertyChanged;

#endif
    }


#if WINDOWS

    private void
        RemoteH264ViewModel_OnPropertyChanged(
            object? sender,
            PropertyChangedEventArgs e)
    {
        if (
            e.PropertyName !=
                nameof(
                    MainWindowViewModel
                        .IsRemoteStreamActive
                )
        )
        {
            return;
        }


        MainWindowViewModel?
            viewModel =
                _remoteH264ViewModel;


        if (
            viewModel is null ||
            viewModel.IsRemoteStreamActive ||
            !_isRemoteFullscreen
        )
        {
            return;
        }


        Dispatcher.UIThread.Post(
            ExitRemoteFullscreen
        );
    }


    private void
        RemoteH264VideoSurface_OnPointerMovedNormalized(
            double normalizedX,
            double normalizedY)
    {
        MainWindowViewModel?
            viewModel =
                _remoteH264ViewModel;


        if (
            viewModel is null ||
            !viewModel.CanControlRemoteHost
        )
        {
            return;
        }


        viewModel.QueueRemoteMouseMove(
            normalizedX,
            normalizedY
        );
    }


    private async void
        RemoteH264VideoSurface_OnLeftButtonPressed()
    {
        MainWindowViewModel?
            viewModel =
                _remoteH264ViewModel;


        if (
            viewModel is null ||
            !viewModel.CanControlRemoteHost
        )
        {
            return;
        }


        //
        // The native GPU HWND owns mouse input,
        // while Avalonia keeps keyboard focus.
        //
        Dispatcher.UIThread.Post(
            () =>
                RemoteInputSurface.Focus()
        );


        await viewModel
            .SendRemoteLeftButtonDownAsync();
    }


    private async void
        RemoteH264VideoSurface_OnLeftButtonReleased()
    {
        MainWindowViewModel?
            viewModel =
                _remoteH264ViewModel;


        if (
            viewModel is null ||
            !viewModel.CanControlRemoteHost
        )
        {
            return;
        }


        await viewModel
            .SendRemoteLeftButtonUpAsync();
    }


    private async void
        RemoteH264VideoSurface_OnMouseWheel(
            int delta)
    {
        MainWindowViewModel?
            viewModel =
                _remoteH264ViewModel;


        if (
            viewModel is null ||
            !viewModel.CanControlRemoteHost ||
            delta == 0
        )
        {
            return;
        }


        await viewModel
            .SendRemoteMouseWheelAsync(
                delta
            );
    }


    private void
        RemoteH264VideoSurface_OnPipelineFaulted(
            Exception exception)
    {
        string logPath =
            Path.Combine(
                AppContext.BaseDirectory,
                "remote-media-error.txt"
            );


        try
        {
            File.WriteAllText(
                logPath,
                exception.ToString()
            );
        }
        catch
        {
            //
            // Diagnostics must never create
            // another application failure.
            //
        }


        Dispatcher.UIThread.Post(
            () =>
            {
                MainWindowViewModel?
                    viewModel =
                        _remoteH264ViewModel;


                if (viewModel is null)
                {
                    return;
                }


                viewModel.IsRemoteStreamActive =
                    false;


                viewModel.RemoteScreenInfo =
                    "Remote video stopped. " +
                    "Remote control is disabled.";


                viewModel.StatusMessage =
                    "Remote video stopped unexpectedly. " +
                    "Restart screen sharing to continue.";


                _ =
                    viewModel
                        .ReleaseViewerRemoteKeysAsync();


                if (_isRemoteFullscreen)
                {
                    ExitRemoteFullscreen();
                }
            }
        );
    }

#endif


    private async void
        MainWindow_OnClosing(
            object? sender,
            WindowClosingEventArgs e)
    {
        if (
            DataContext is
                MainWindowViewModel
                    viewModel
        )
        {
            viewModel.H264VideoSubmitter =
                null;


            await viewModel
                .ReleaseViewerRemoteKeysAsync();
        }


#if WINDOWS

        RemoteVideoSurface?
            remoteSurface =
                _remoteH264VideoSurface;


        MainWindowViewModel?
            remoteViewModel =
                _remoteH264ViewModel;


        if (remoteSurface is not null)
        {
            remoteSurface.PointerMovedNormalized -=
                RemoteH264VideoSurface_OnPointerMovedNormalized;


            remoteSurface.LeftButtonPressed -=
                RemoteH264VideoSurface_OnLeftButtonPressed;


            remoteSurface.LeftButtonReleased -=
                RemoteH264VideoSurface_OnLeftButtonReleased;


            remoteSurface.MouseWheel -=
                RemoteH264VideoSurface_OnMouseWheel;


            remoteSurface.PipelineFaulted -=
                RemoteH264VideoSurface_OnPipelineFaulted;
        }


        if (remoteViewModel is not null)
        {
            remoteViewModel.PropertyChanged -=
                RemoteH264ViewModel_OnPropertyChanged;
        }


        _remoteH264VideoSurface =
            null;


        _remoteH264ViewModel =
            null;

#endif
    }


    private async void
        RemoteInputSurface_OnKeyDown(
            object? sender,
            KeyEventArgs e)
    {
        if (
            DataContext is not
                MainWindowViewModel
                    viewModel
        )
        {
            return;
        }


        //
        // Keep Escape available for the remote host.
        // Ctrl+Shift+F is the local fullscreen toggle.
        //
        if (
            _isRemoteFullscreen &&
            e.Key ==
                Key.F &&
            e.KeyModifiers.HasFlag(
                KeyModifiers.Control
            ) &&
            e.KeyModifiers.HasFlag(
                KeyModifiers.Shift
            )
        )
        {
            await viewModel
                .ReleaseViewerRemoteKeysAsync();


            ExitRemoteFullscreen();


            e.Handled =
                true;


            return;
        }


        if (!viewModel.CanControlRemoteHost)
        {
            return;
        }


        if (
            !RemoteKeyMapper.TryMap(
                e.Key,
                out RemoteKey remoteKey
            )
        )
        {
            return;
        }


        await viewModel
            .SendRemoteKeyDownAsync(
                remoteKey
            );


        e.Handled =
            true;
    }


    private async void
        RemoteInputSurface_OnKeyUp(
            object? sender,
            KeyEventArgs e)
    {
        if (
            DataContext is not
                MainWindowViewModel
                    viewModel
        )
        {
            return;
        }


        if (!viewModel.CanControlRemoteHost)
        {
            return;
        }


        if (
            !RemoteKeyMapper.TryMap(
                e.Key,
                out RemoteKey remoteKey
            )
        )
        {
            return;
        }


        await viewModel
            .SendRemoteKeyUpAsync(
                remoteKey
            );


        e.Handled =
            true;
    }


    private async void
        RemoteInputSurface_OnLostFocus(
            object? sender,
            RoutedEventArgs e)
    {
        if (
            DataContext is
                MainWindowViewModel
                    viewModel
        )
        {
            await viewModel
                .ReleaseViewerRemoteKeysAsync();
        }
    }


    private async void
        ViewFullScreen_OnClick(
            object? sender,
            RoutedEventArgs e)
    {
        if (
            DataContext is not
                MainWindowViewModel
                    viewModel
        )
        {
            return;
        }


        if (
            !viewModel.IsViewerConnected ||
            !viewModel.IsRemoteStreamActive
        )
        {
            return;
        }


        await viewModel
            .ReleaseViewerRemoteKeysAsync();


        EnterRemoteFullscreen();
    }


    private void
        EnterRemoteFullscreen()
    {
        if (_isRemoteFullscreen)
        {
            return;
        }


        _isRemoteFullscreen =
            true;


        _windowStateBeforeRemoteFullscreen =
            WindowState;


        _mainContentWidthBeforeFullscreen =
            MainContentPanel.Width;


        _mainContentMarginBeforeFullscreen =
            MainContentPanel.Margin;


        _remoteInputHeightBeforeFullscreen =
            RemoteInputSurface.Height;


        _remoteInputMinHeightBeforeFullscreen =
            RemoteInputSurface.MinHeight;


        _scrollOffsetBeforeFullscreen =
            MainScrollViewer.Offset;


        _remoteScreenPaddingBeforeFullscreen =
            RemoteScreenCard.Padding;


        _remoteScreenBorderBeforeFullscreen =
            RemoteScreenCard.BorderThickness;


        _remoteScreenCornerRadiusBeforeFullscreen =
            RemoteScreenCard.CornerRadius;


        //
        // Reuse the existing GPU surface rather than
        // creating a second decoder/presenter.
        //
        MainContentPanel.Width =
            double.NaN;


        MainContentPanel.Margin =
            new Avalonia.Thickness(
                0
            );


        MainContentPanel.HorizontalAlignment =
            Avalonia.Layout
                .HorizontalAlignment
                .Stretch;


        RemoteScreenCard.Padding =
            new Avalonia.Thickness(
                0
            );


        RemoteScreenCard.BorderThickness =
            new Avalonia.Thickness(
                0
            );


        RemoteScreenCard.CornerRadius =
            new Avalonia.CornerRadius(
                0
            );


        RemoteInputSurface.MinHeight =
            0;


        WindowState =
            WindowState.FullScreen;


        Dispatcher.UIThread.Post(
            () =>
            {
                if (!_isRemoteFullscreen)
                {
                    return;
                }


                RemoteInputSurface.Height =
                    Math.Max(
                        1,
                        Bounds.Height
                    );


                RemoteInputSurface
                    .BringIntoView();


                RemoteInputSurface
                    .Focus();
            },
            DispatcherPriority.Loaded
        );
    }


    private void
        ExitRemoteFullscreen()
    {
        if (!_isRemoteFullscreen)
        {
            return;
        }


        _isRemoteFullscreen =
            false;


        WindowState =
            _windowStateBeforeRemoteFullscreen;


        MainContentPanel.Width =
            _mainContentWidthBeforeFullscreen;


        MainContentPanel.Margin =
            _mainContentMarginBeforeFullscreen;


        MainContentPanel.HorizontalAlignment =
            Avalonia.Layout
                .HorizontalAlignment
                .Center;


        RemoteScreenCard.Padding =
            _remoteScreenPaddingBeforeFullscreen;


        RemoteScreenCard.BorderThickness =
            _remoteScreenBorderBeforeFullscreen;


        RemoteScreenCard.CornerRadius =
            _remoteScreenCornerRadiusBeforeFullscreen;


        RemoteInputSurface.Height =
            _remoteInputHeightBeforeFullscreen;


        RemoteInputSurface.MinHeight =
            _remoteInputMinHeightBeforeFullscreen;


        Dispatcher.UIThread.Post(
            () =>
            {
                MainScrollViewer.Offset =
                    _scrollOffsetBeforeFullscreen;


                RemoteInputSurface
                    .Focus();
            },
            DispatcherPriority.Loaded
        );
    }


    private void
        MainWindow_OnSizeChanged(
            object? sender,
            SizeChangedEventArgs e)
    {
        if (!_isRemoteFullscreen)
        {
            return;
        }


        RemoteInputSurface.Height =
            Math.Max(
                1,
                e.NewSize.Height
            );
    }
}