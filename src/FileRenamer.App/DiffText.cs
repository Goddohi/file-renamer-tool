using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using FileRenamer.Core;

namespace FileRenamer.App;

/// <summary>TextBlock에 이름 변경 구간을 그린다. 지워지는 글자는 취소선, 새로 들어가는 글자는 강조 배경.</summary>
public static class DiffText
{
    public static readonly DependencyProperty SegmentsProperty = DependencyProperty.RegisterAttached(
        "Segments", typeof(IReadOnlyList<DiffSegment>), typeof(DiffText), new PropertyMetadata(null, OnSegmentsChanged));

    public static IReadOnlyList<DiffSegment>? GetSegments(DependencyObject d) => (IReadOnlyList<DiffSegment>?)d.GetValue(SegmentsProperty);
    public static void SetSegments(DependencyObject d, IReadOnlyList<DiffSegment>? value) => d.SetValue(SegmentsProperty, value);

    private static void OnSegmentsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock tb) return;
        tb.Inlines.Clear();
        if (e.NewValue is not IReadOnlyList<DiffSegment> segs) return;
        foreach (var s in segs)
        {
            var run = new Run(s.Text);
            switch (s.Kind)
            {
                case DiffKind.Removed:
                    run.SetResourceReference(TextElement.BackgroundProperty, "DelBgBrush");
                    run.SetResourceReference(TextElement.ForegroundProperty, "DelFgBrush");
                    run.TextDecorations = TextDecorations.Strikethrough;
                    break;
                case DiffKind.Added:
                    run.SetResourceReference(TextElement.BackgroundProperty, "AddBgBrush");
                    run.SetResourceReference(TextElement.ForegroundProperty, "AddFgBrush");
                    run.FontWeight = FontWeights.SemiBold;
                    break;
            }
            tb.Inlines.Add(run);
        }
    }
}
