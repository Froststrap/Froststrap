// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.VisualTree;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Froststrap.UI.Elements.Controls
{
    internal sealed class AotDataGridColumn : AvaloniaObject
    {
        public static readonly StyledProperty<object?> HeaderProperty =
            AvaloniaProperty.Register<AotDataGridColumn, object?>(nameof(Header));

        public static readonly StyledProperty<GridLength> WidthProperty =
            AvaloniaProperty.Register<AotDataGridColumn, GridLength>(nameof(Width), GridLength.Star);

        public static readonly StyledProperty<double> MinWidthProperty =
            AvaloniaProperty.Register<AotDataGridColumn, double>(nameof(MinWidth), 40d);

        public static readonly StyledProperty<bool> CanUserResizeProperty =
            AvaloniaProperty.Register<AotDataGridColumn, bool>(nameof(CanUserResize), true);

        public static readonly StyledProperty<bool> CanUserSortProperty =
            AvaloniaProperty.Register<AotDataGridColumn, bool>(nameof(CanUserSort), true);

        public static readonly StyledProperty<bool> IsVisibleProperty =
            AvaloniaProperty.Register<AotDataGridColumn, bool>(nameof(IsVisible), true);

        public static readonly StyledProperty<IDataTemplate?> CellTemplateProperty =
            AvaloniaProperty.Register<AotDataGridColumn, IDataTemplate?>(nameof(CellTemplate));

        public object? Header
        {
            get => GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        public GridLength Width
        {
            get => GetValue(WidthProperty);
            set => SetValue(WidthProperty, value);
        }

        public double MinWidth
        {
            get => GetValue(MinWidthProperty);
            set => SetValue(MinWidthProperty, value);
        }

        public bool CanUserResize
        {
            get => GetValue(CanUserResizeProperty);
            set => SetValue(CanUserResizeProperty, value);
        }

        public bool CanUserSort
        {
            get => GetValue(CanUserSortProperty);
            set => SetValue(CanUserSortProperty, value);
        }

        public bool IsVisible
        {
            get => GetValue(IsVisibleProperty);
            set => SetValue(IsVisibleProperty, value);
        }

        public IDataTemplate? CellTemplate
        {
            get => GetValue(CellTemplateProperty);
            set => SetValue(CellTemplateProperty, value);
        }

        public Func<object?, IComparable?>? SortKey { get; set; }
    }

    internal sealed class AotDataGrid : UserControl
    {
        public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
            AvaloniaProperty.Register<AotDataGrid, IEnumerable?>(nameof(ItemsSource));

        public static readonly StyledProperty<double> RowHeightProperty =
            AvaloniaProperty.Register<AotDataGrid, double>(nameof(RowHeight), 30d);

        private readonly Grid _root = new() { Name = "PART_Root" };
        private readonly Border _headerBorder = new() { Name = "PART_HeaderBorder" };
        private readonly Grid _headerGrid = new();
        private readonly ListBox _listBox = new() { Name = "PART_ListBox" };
        private readonly ObservableCollection<object?> _view = [];
        private readonly List<(AotDataGridColumn Column, WeakReference<ColumnDefinition> Definition)> _definitions = [];

        private IEnumerable? _source;
        private INotifyCollectionChanged? _sourceNotifier;
        private AotDataGridColumn? _sortColumn;
        private bool _sortDescending;
        private bool _rebuilding;
        private double _headerScrollbarOffset = -1;

        public AotDataGrid()
        {
            _root.RowDefinitions = new RowDefinitions("Auto,*");

            _headerBorder.Child = _headerGrid;
            Grid.SetRow(_headerBorder, 0);
            _root.Children.Add(_headerBorder);

            _listBox.ItemsSource = _view;
            _listBox.ItemTemplate = new FuncDataTemplate(_ => true, (item, _) => BuildRow(item), false);
            _listBox.SelectionChanged += (_, e) => SelectionChanged?.Invoke(this, e);
            _listBox.LayoutUpdated += (_, _) => UpdateHeaderOffset();
            Grid.SetRow(_listBox, 1);
            _root.Children.Add(_listBox);

            Content = _root;

            Columns.CollectionChanged += OnColumnsChanged;
        }

        public AvaloniaList<AotDataGridColumn> Columns { get; } = [];

        public IEnumerable? ItemsSource
        {
            get => GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public double RowHeight
        {
            get => GetValue(RowHeightProperty);
            set => SetValue(RowHeightProperty, value);
        }

        public SelectionMode SelectionMode
        {
            get => _listBox.SelectionMode;
            set => _listBox.SelectionMode = value;
        }

        public object? SelectedItem
        {
            get => _listBox.SelectedItem;
            set => _listBox.SelectedItem = value;
        }

        public IList? SelectedItems => _listBox.SelectedItems;

        public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;

        public void ScrollIntoView(object? item)
        {
            if (item is not null)
                _listBox.ScrollIntoView(item);
        }

        public void SortBy(AotDataGridColumn column, bool descending)
        {
            _sortColumn = column;
            _sortDescending = descending;

            SortView();
            RebuildHeader();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ItemsSourceProperty)
                AttachSource(change.NewValue as IEnumerable);
            else if (change.Property == RowHeightProperty)
                RebuildView();
        }

        private void AttachSource(IEnumerable? source)
        {
            if (_sourceNotifier is not null)
                _sourceNotifier.CollectionChanged -= OnSourceCollectionChanged;

            _source = source;
            _sourceNotifier = source as INotifyCollectionChanged;

            if (_sourceNotifier is not null)
                _sourceNotifier.CollectionChanged += OnSourceCollectionChanged;

            RebuildView();
        }

        private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add when e.NewItems is not null:
                    foreach (var item in e.NewItems)
                        InsertIntoView(item);
                    break;

                case NotifyCollectionChangedAction.Remove when e.OldItems is not null:
                    foreach (var item in e.OldItems)
                    {
                        int index = _view.IndexOf(item);
                        if (index >= 0)
                            _view.RemoveAt(index);
                    }
                    break;

                default:
                    RebuildView();
                    break;
            }
        }

        private void RebuildView()
        {
            if (_rebuilding)
                return;

            _rebuilding = true;

            try
            {
                var selected = _listBox.SelectedItems?.OfType<object?>().ToList() ?? [];

                _view.Clear();

                if (_source is not null) foreach (var item in _source)
                    _view.Add(item);

                SortView();

                if (selected.Count > 0 && _listBox.SelectedItems is { } selectedItems) foreach (var item in selected)
                {
                    if (_view.Contains(item))
                        selectedItems.Add(item);
                }
            }
            finally
            {
                _rebuilding = false;
            }
        }

        private void InsertIntoView(object? item)
        {
            if (_sortColumn?.SortKey is null)
            {
                _view.Add(item);
                return;
            }

            int index = 0;
            while (index < _view.Count && CompareItems(_view[index], item) <= 0)
                index++;

            _view.Insert(index, item);
        }

        private void SortView()
        {
            if (_sortColumn?.SortKey is null || _view.Count < 2)
                return;

            var sorted = _view.ToList();
            sorted.Sort(CompareItems);

            _view.Clear();
            foreach (var item in sorted)
                _view.Add(item);
        }

        private int CompareItems(object? x, object? y)
        {
            if (_sortColumn?.SortKey is not { } key)
                return 0;

            var left = key(x);
            var right = key(y);

            int result;
            if (left is null && right is null)
                result = 0;
            else if (left is null)
                result = -1;
            else if (right is null)
                result = 1;
            else if (left is string leftString && right is string rightString)
                result = StringComparer.CurrentCultureIgnoreCase.Compare(leftString, rightString);
            else
                result = left.CompareTo(right);

            return _sortDescending ? -result : result;
        }

        private void HandleHeaderClick(AotDataGridColumn column)
        {
            if (!column.CanUserSort || column.SortKey is null)
                return;

            if (ReferenceEquals(_sortColumn, column))
                _sortDescending = !_sortDescending;
            else
            {
                _sortColumn = column;
                _sortDescending = false;
            }

            SortView();
            RebuildHeader();
        }

        private void OnColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems is not null)
            {
                foreach (AotDataGridColumn column in e.OldItems)
                    column.PropertyChanged -= OnColumnPropertyChanged;
            }

            if (e.NewItems is not null)
            {
                foreach (AotDataGridColumn column in e.NewItems)
                    column.PropertyChanged += OnColumnPropertyChanged;
            }

            RefreshStructure();
        }

        private void OnColumnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (sender is not AotDataGridColumn column)
                return;

            if (e.Property == AotDataGridColumn.WidthProperty || e.Property == AotDataGridColumn.MinWidthProperty)
            {
                UpdateColumnDefinitions(column);
            }
            else if (e.Property == AotDataGridColumn.IsVisibleProperty ||
                     e.Property == AotDataGridColumn.CellTemplateProperty ||
                     e.Property == AotDataGridColumn.CanUserResizeProperty ||
                     e.Property == AotDataGridColumn.CanUserSortProperty ||
                     e.Property == AotDataGridColumn.HeaderProperty)
            {
                RefreshStructure();
            }
        }

        private void RefreshStructure()
        {
            RebuildHeader();
            RebuildView();
        }

        private void RebuildHeader()
        {
            _headerGrid.Children.Clear();
            _headerGrid.ColumnDefinitions.Clear();

            int index = 0;
            foreach (var column in Columns)
            {
                if (!column.IsVisible)
                    continue;

                var definition = new ColumnDefinition
                {
                    Width = column.Width,
                    MinWidth = column.MinWidth
                };

                _headerGrid.ColumnDefinitions.Add(definition);
                RegisterDefinition(column, definition);

                var cell = BuildHeaderCell(column, definition);
                Grid.SetColumn(cell, index);
                _headerGrid.Children.Add(cell);

                index++;
            }
        }

        private Grid BuildHeaderCell(AotDataGridColumn column, ColumnDefinition definition)
        {
            bool isSorted = ReferenceEquals(_sortColumn, column);

            var button = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            button.Classes.Add("header");

            if (isSorted)
                button.Classes.Add("sorted");

            var content = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };

            var headerContent = new ContentControl
            {
                Content = column.Header,
                VerticalAlignment = VerticalAlignment.Center
            };
            content.Children.Add(headerContent);

            var arrow = new TextBlock
            {
                Text = isSorted ? (_sortDescending ? "▼" : "▲") : string.Empty,
                FontSize = 10,
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(arrow, 1);
            content.Children.Add(arrow);

            button.Content = content;
            button.Click += (_, _) => HandleHeaderClick(column);

            var cell = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            Grid.SetColumn(button, 0);
            cell.Children.Add(button);

            if (column.CanUserResize)
            {
                var handle = new Border
                {
                    Width = 9,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Cursor = new Cursor(StandardCursorType.SizeWestEast)
                };
                handle.Classes.Add("resize-handle");
                Grid.SetColumn(handle, 1);

                var grip = new Border
                {
                    Width = 1,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Margin = new Thickness(0, 6, 0, 6)
                };
                grip.Classes.Add("resize-grip");
                handle.Child = grip;

                bool dragging = false;
                double dragStartX = 0;
                double dragStartWidth = 0;

                handle.PointerPressed += (_, e) =>
                {
                    var point = e.GetCurrentPoint(_headerGrid);
                    if (!point.Properties.IsLeftButtonPressed)
                        return;

                    dragging = true;
                    dragStartX = point.Position.X;
                    dragStartWidth = definition.ActualWidth;
                    e.Pointer.Capture(handle);
                    e.Handled = true;
                };

                handle.PointerMoved += (_, e) =>
                {
                    if (!dragging)
                        return;

                    var point = e.GetCurrentPoint(_headerGrid);
                    double newWidth = Math.Max(column.MinWidth, dragStartWidth + (point.Position.X - dragStartX));
                    column.Width = new GridLength(newWidth);
                    e.Handled = true;
                };

                handle.PointerReleased += (_, e) =>
                {
                    if (!dragging)
                        return;

                    dragging = false;
                    e.Pointer.Capture(null);
                    e.Handled = true;
                };

                handle.PointerCaptureLost += (_, _) => dragging = false;

                cell.Children.Add(handle);
            }

            return cell;
        }

        private Grid BuildRow(object? item)
        {
            var grid = new Grid { MinHeight = RowHeight };

            int index = 0;
            foreach (var column in Columns)
            {
                if (!column.IsVisible)
                    continue;

                var definition = new ColumnDefinition
                {
                    Width = column.Width,
                    MinWidth = column.MinWidth
                };

                grid.ColumnDefinitions.Add(definition);
                RegisterDefinition(column, definition);

                var cell = new ContentControl
                {
                    Content = item,
                    ContentTemplate = column.CellTemplate,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 0, 6, 0)
                };

                Grid.SetColumn(cell, index);
                grid.Children.Add(cell);

                index++;
            }

            return grid;
        }

        private void RegisterDefinition(AotDataGridColumn column, ColumnDefinition definition)
        {
            _definitions.Add((column, new WeakReference<ColumnDefinition>(definition)));
        }

        private void UpdateColumnDefinitions(AotDataGridColumn column)
        {
            for (int i = _definitions.Count - 1; i >= 0; i--)
            {
                var entry = _definitions[i];
                if (!entry.Definition.TryGetTarget(out var definition))
                {
                    _definitions.RemoveAt(i);
                    continue;
                }

                if (!ReferenceEquals(entry.Column, column))
                    continue;

                definition.Width = column.Width;
                definition.MinWidth = column.MinWidth;
            }
        }

        private void UpdateHeaderOffset()
        {
            var scrollViewer = _listBox.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
            double offset = scrollViewer is null
                ? 0
                : Math.Max(0, _listBox.Bounds.Width - scrollViewer.Viewport.Width);

            if (Math.Abs(offset - _headerScrollbarOffset) < 0.5)
                return;

            _headerScrollbarOffset = offset;
            _headerBorder.Margin = new Thickness(0, 0, offset, 0);
        }
    }
}
