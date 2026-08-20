using System;
using System.Windows;
using System.Windows.Input;
using Emignatik.NxFileViewer.Styling.Theme;
using Emignatik.NxFileViewer.Utils.MVVM.Commands;
using Emignatik.NxFileViewer.Views.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace Emignatik.NxFileViewer.Commands;

public class ShowRenameToolWindowCommand : CommandBase, IShowRenameToolWindowCommand
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IThemeService _themeService;
    private RenameToolWindow? _renameToolWindow;

    public ShowRenameToolWindowCommand(IServiceProvider serviceProvider, IThemeService themeService)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
    }

    public override void Execute(object? parameter)
    {
        if (_renameToolWindow != null)
        {
            _renameToolWindow.Activate();
            return;
        }

        var renameToolWindowViewModel = _serviceProvider.GetRequiredService<RenameToolWindowViewModel>();

        _renameToolWindow = new RenameToolWindow
        {
            Owner = Application.Current.MainWindow,
        };
        _renameToolWindow.Closed += OnRenameToolWindowClosed;
        _renameToolWindow.DataContext = renameToolWindowViewModel;
        renameToolWindowViewModel.Window = _renameToolWindow;
        _themeService.RegisterWindow(_renameToolWindow);

        _renameToolWindow.Show();
    }

    private void OnRenameToolWindowClosed(object? sender, EventArgs e)
    {
        _renameToolWindow = null;
        TriggerCanExecuteChanged();
    }
}

public interface IShowRenameToolWindowCommand : ICommand
{
}
