using DocumentManagement.Models;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;

namespace DocumentManagement.Services;

/// <summary>Builds a lightweight organization chart image from the per-contract reporting structure.</summary>
public sealed class OrganizationChartRenderer
{
    private const double NodeWidth = 220;
    private const double NodeHeight = 72;
    private const double HorizontalGap = 42;
    private const double VerticalGap = 54;
    private const double Margin = 36;

    public byte[] Render(IEnumerable<HsplPerson> personnel)
    {
        var people = personnel
            .Where(person => !string.IsNullOrWhiteSpace(person.FullName) && !string.IsNullOrWhiteSpace(person.Position))
            .ToList();
        if (people.Count == 0) return [];

        var nodes = people.ToDictionary(person => person.Id, person => new ChartNode(person));
        var roots = new List<ChartNode>();
        foreach (var node in nodes.Values)
        {
            if (!string.IsNullOrWhiteSpace(node.Person.ReportsToId)
                && node.Person.ReportsToId != node.Person.Id
                && nodes.TryGetValue(node.Person.ReportsToId, out var parent)
                && !WouldCreateCycle(node, parent))
            {
                parent.Children.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }

        if (roots.Count == 0) roots.AddRange(nodes.Values);
        var leafIndex = 0;
        var maxDepth = 0;
        foreach (var root in roots)
            Measure(root, 0, ref leafIndex, ref maxDepth);

        var width = Math.Max(760, Margin * 2 + Math.Max(1, leafIndex) * NodeWidth + Math.Max(0, leafIndex - 1) * HorizontalGap);
        var height = Math.Max(280, Margin * 2 + (maxDepth + 1) * NodeHeight + maxDepth * VerticalGap);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
            foreach (var root in roots) DrawConnections(context, root);
            foreach (var root in roots) DrawNodes(context, root);
        }

        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(width), (int)Math.Ceiling(height), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static bool WouldCreateCycle(ChartNode node, ChartNode parent)
    {
        for (var current = parent; current != null; current = current.Parent)
        {
            if (ReferenceEquals(current, node)) return true;
        }

        node.Parent = parent;
        return false;
    }

    private static void Measure(ChartNode node, int depth, ref int leafIndex, ref int maxDepth)
    {
        node.Depth = depth;
        maxDepth = Math.Max(maxDepth, depth);
        if (node.Children.Count == 0)
        {
            node.LeafCenter = leafIndex++;
            return;
        }

        foreach (var child in node.Children)
            Measure(child, depth + 1, ref leafIndex, ref maxDepth);
        node.LeafCenter = (node.Children.First().LeafCenter + node.Children.Last().LeafCenter) / 2d;
    }

    private static Point Position(ChartNode node) => new(
        Margin + node.LeafCenter * (NodeWidth + HorizontalGap),
        Margin + node.Depth * (NodeHeight + VerticalGap));

    private static void DrawConnections(DrawingContext context, ChartNode node)
    {
        var source = Position(node);
        foreach (var child in node.Children)
        {
            var target = Position(child);
            var start = new Point(source.X + NodeWidth / 2, source.Y + NodeHeight);
            var end = new Point(target.X + NodeWidth / 2, target.Y);
            var middleY = (start.Y + end.Y) / 2;
            var geometry = new StreamGeometry();
            using (var figure = geometry.Open())
            {
                figure.BeginFigure(start, false, false);
                figure.LineTo(new Point(start.X, middleY), true, false);
                figure.LineTo(new Point(end.X, middleY), true, false);
                figure.LineTo(end, true, false);
            }
            context.DrawGeometry(null, new Pen(Brushes.Black, 1.2), geometry);
            DrawArrow(context, end);
            DrawConnections(context, child);
        }
    }

    private static void DrawArrow(DrawingContext context, Point end)
    {
        var triangle = new StreamGeometry();
        using var figure = triangle.Open();
        figure.BeginFigure(end, true, true);
        figure.LineTo(new Point(end.X - 4, end.Y - 8), true, false);
        figure.LineTo(new Point(end.X + 4, end.Y - 8), true, false);
        context.DrawGeometry(Brushes.Black, null, triangle);
    }

    private static void DrawNodes(DrawingContext context, ChartNode node)
    {
        var position = Position(node);
        context.DrawRoundedRectangle(Brushes.White, new Pen(Brushes.Black, 1.2), new Rect(position.X, position.Y, NodeWidth, NodeHeight), 2, 2);
        DrawCenteredText(context, node.Person.Position, new Point(position.X + NodeWidth / 2, position.Y + 18), 12, FontWeights.Bold);
        DrawCenteredText(context, node.Person.FullName, new Point(position.X + NodeWidth / 2, position.Y + 46), 11, FontWeights.Normal);
        foreach (var child in node.Children) DrawNodes(context, child);
    }

    private static void DrawCenteredText(DrawingContext context, string value, Point center, double fontSize, FontWeight weight)
    {
        var text = new FormattedText(value, System.Globalization.CultureInfo.GetCultureInfo("vi-VN"), FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Times New Roman"), FontStyles.Normal, weight, FontStretches.Normal), fontSize, Brushes.Black, 1.0);
        context.DrawText(text, new Point(center.X - text.Width / 2, center.Y - text.Height / 2));
    }

    private sealed class ChartNode(HsplPerson person)
    {
        public HsplPerson Person { get; } = person;
        public ChartNode? Parent { get; set; }
        public List<ChartNode> Children { get; } = [];
        public int Depth { get; set; }
        public double LeafCenter { get; set; }
    }
}
