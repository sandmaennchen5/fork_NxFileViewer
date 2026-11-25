using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;

namespace NxFileViewer.Avalonia;

public partial class MainWindow
{
    private readonly List<TabItem> _workspaceJobs = new();

    private IDisposable LockWorkspace(TabItem owner)
    {
        _workspaceJobs.Add(owner);
        RefreshWorkspaceLock();
        return new WorkspaceJob(this, owner);
    }

    private void RefreshWorkspaceLock()
    {
        if (_closed) return;
        var owner = _workspaceJobs.FirstOrDefault();
        BatchMeasureMemory.IsEnabled = owner == null && _batchMemoryDiagnostics?.IsEnabled == true;
        foreach (var tab in Workspace.Items.OfType<TabItem>())
            tab.IsEnabled = owner == null || tab == owner || tab == LogTab;
        if (owner != null && Workspace.SelectedItem != owner && Workspace.SelectedItem != LogTab)
            Workspace.SelectedItem = owner;
        RefreshEntryActions();
    }

    private sealed class WorkspaceJob(MainWindow window, TabItem owner) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            window._workspaceJobs.Remove(owner);
            window.RefreshWorkspaceLock();
        }
    }
}
