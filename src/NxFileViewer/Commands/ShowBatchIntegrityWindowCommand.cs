using System;
using System.Windows;
using System.Windows.Input;
using Emignatik.NxFileViewer.Utils.MVVM.Commands;
using Emignatik.NxFileViewer.Styling.Theme;
using Emignatik.NxFileViewer.Views.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Emignatik.NxFileViewer.Commands;

public sealed class ShowBatchIntegrityWindowCommand : CommandBase, IShowBatchIntegrityWindowCommand
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IThemeService _themeService;
    private readonly ILogger<ShowBatchIntegrityWindowCommand> _logger;
    private BatchIntegrityWindow? _window;

    public ShowBatchIntegrityWindowCommand(IServiceProvider serviceProvider, IThemeService themeService,
        ILogger<ShowBatchIntegrityWindowCommand> logger)
    {
        _serviceProvider = serviceProvider;
        _themeService = themeService;
        _logger = logger;
    }

    public override void Execute(object? parameter)
    {
        if (_window != null) { _window.Activate(); return; }
        try
        {
            var viewModel = _serviceProvider.GetRequiredService<BatchIntegrityWindowViewModel>();
            _window = new BatchIntegrityWindow { Owner = Application.Current.MainWindow, DataContext = viewModel };
            _themeService.RegisterWindow(_window);
            viewModel.Window = _window;
            _window.Closed += (_, _) => _window = null;
            _window.Show();
        }
        catch (Exception ex)
        {
            _window = null;
            _logger.LogError(ex, "Failed to open the batch integrity window.");
            MessageBox.Show(Application.Current.MainWindow, ex.Message, "NxFileViewer",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

public interface IShowBatchIntegrityWindowCommand : ICommand { }
