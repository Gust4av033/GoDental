using System.Windows;
using System.Windows.Threading;
using GoDental.Application.Abstractions.Security;
using GoDental.Wpf.ViewModels;

namespace GoDental.Wpf.Navigation;

public sealed class WindowCoordinator : IDisposable
{
    private readonly System.Windows.Application application;
    private readonly LoginWindow login;
    private readonly MainWindow workspace;
    private readonly ISessionController session;
    private readonly AccessViewModel access;
    private bool shuttingDown;

    public WindowCoordinator(System.Windows.Application application, LoginWindow login, MainWindow workspace,
        ISessionController session, AccessViewModel access)
    {
        this.application = application;
        this.login = login;
        this.workspace = workspace;
        this.session = session;
        this.access = access;
        login.Closed += OnWindowClosed;
        workspace.Closed += OnWindowClosed;
        session.Changed += OnSessionChanged;
    }

    public void Start()
    {
        application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        FitToWorkArea(login);
        FitToWorkArea(workspace);
        ShowCurrentWindow();
        PrepareAccessIfNeeded();
    }

    private static void FitToWorkArea(Window window)
    {
        var bounds = SystemParameters.WorkArea;
        window.Width = Math.Min(window.Width, bounds.Width * 0.96);
        window.Height = Math.Min(window.Height, bounds.Height * 0.96);
    }

    private void OnSessionChanged(object? sender, EventArgs e)
    {
        ShowCurrentWindow();
        if (!session.IsAuthenticated)
            application.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(PrepareAccessIfNeeded));
    }

    private void ShowCurrentWindow()
    {
        if (session.IsAuthenticated)
        {
            application.MainWindow = workspace;
            workspace.Show();
            login.Hide();
        }
        else
        {
            application.MainWindow = login;
            login.Show();
            workspace.Hide();
        }
    }

    private void PrepareAccessIfNeeded()
    {
        if (!session.IsAuthenticated && !access.IsReady && !access.MustChangePassword && !access.IsBusy)
            access.PrepareCommand.Execute(null);
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (shuttingDown || !ReferenceEquals(application.MainWindow, sender)) return;
        shuttingDown = true;
        application.Shutdown();
    }

    public void Dispose()
    {
        login.Closed -= OnWindowClosed;
        workspace.Closed -= OnWindowClosed;
        session.Changed -= OnSessionChanged;
    }
}
