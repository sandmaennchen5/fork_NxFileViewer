using System;
using System.Windows;
using System.Windows.Input;
using Emignatik.NxFileViewer.Styling.Theme;
using Emignatik.NxFileViewer.Utils.MVVM.Commands;
using Emignatik.NxFileViewer.Views.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace Emignatik.NxFileViewer.Commands;

public class ShowSettingsWindowCommand : CommandBase, IShowSettingsWindowCommand
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IThemeService _themeService;
    private SettingsWindow? _settingsWindow;

    public ShowSettingsWindowCommand(IServiceProvider serviceProvider, IThemeService themeService)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
    }

    public override void Execute(object? parameter)
    {
        if (_settingsWindow != null)
        {
            _settingsWindow.Activate();
            return;
        }

        var settingsWindowViewModel = _serviceProvider.GetRequiredService<SettingsWindowViewModel>();

        _settingsWindow = new SettingsWindow
        {
            Owner = Application.Current.MainWindow,
        };
        _settingsWindow.Closed += OnSettingsWindowClosed;
        _settingsWindow.DataContext = settingsWindowViewModel;
        settingsWindowViewModel.Window = _settingsWindow;
        _themeService.RegisterWindow(_settingsWindow);

        _settingsWindow.Show();
    }

    private void OnSettingsWindowClosed(object? sender, EventArgs e)
    {
        _settingsWindow = null;
        TriggerCanExecuteChanged();
    }
}

public interface IShowSettingsWindowCommand : ICommand
{
}
