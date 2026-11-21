using System;
using System.Linq;
using System.Xml.Linq;
using Avalonia.Media;
namespace NxFileViewer.Avalonia;
internal static class WorkspaceIcons
{
    internal static IImage Read(string name)
    {
        using var stream = typeof(WorkspaceIcons).Assembly.GetManifestResourceStream("Viewer.WorkspaceIcons")!;
        var xml = XElement.Load(stream);
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var source = xml.Descendants().Single(element => (string?)element.Attribute(x + "Key") == name);
        var group = new DrawingGroup();
        foreach (var element in source.Descendants().Where(element => element.Name.LocalName == "GeometryDrawing"))
        {
            var drawing = new GeometryDrawing { Brush = Brush.Parse((string)element.Attribute("Brush")!), Geometry = Geometry.Parse((string)element.Attribute("Geometry")!) };
            var pen = element.Descendants().FirstOrDefault(child => child.Name.LocalName == "Pen");
            if (pen != null) drawing.Pen = new Pen(Brush.Parse((string)pen.Attribute("Brush")!), double.Parse((string)pen.Attribute("Thickness")!, System.Globalization.CultureInfo.InvariantCulture));
            group.Children.Add(drawing);
        }
        return new DrawingImage(group);
    }
}
