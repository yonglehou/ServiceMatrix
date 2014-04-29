﻿namespace NServiceBusStudio.Automation.CustomSolutionBuilder.Views
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.ComponentModel;
    using NuPattern.Presentation;
    using System.Reflection;
    using NServiceBusStudio.Automation.CustomSolutionBuilder.ViewModels;
    using NuPattern.Runtime.UI.ViewModels;
    using System.Threading;

    /// <summary>
    /// Interaction logic for LogicalView.xaml
    /// </summary>
    public partial class LogicalView
    {
        public LogicalView(IServiceProvider serviceProvider, object myContext)
        {
            var myDataContext = new LogicalViewModel(myContext as ISolutionBuilderViewModel);
            DataContext = myDataContext;
            InitializeComponent();
            //this.DataContext = myContext;
            //var componentModel = ServiceProvider.GlobalProvider.GetService<SComponentModel, IComponentModel>();
            //var container = componentModel.DefaultExportProvider as CompositionContainer;
            //if (container != null)
            //{
            //    container.ComposeExportedValue(this);
            //}
        }

        public LogicalViewModel ViewModel
        {
            get
            {
                return (LogicalViewModel)DataContext;
            }
        }

        public LogicalView(LogicalViewModel myContext)
        {
            DataContext = myContext;
            myContext.FocusOnViewRequested += (s, e) => 
            {
                new Thread(() =>
                    {
                        Dispatcher.Invoke(new Action(() =>
                            {
                                var selectedNode = explorer.Focus();
                                explorer.Focus();
                            }));
                    }).Start();
            };
            InitializeComponent();
        }


        public void InitializeViewSelector()
        {
            ViewSelector.SelectedIndex = 0;
            ViewSelectorBorder.Visibility = Visibility.Visible;
            TitleGrid.Visibility = Visibility.Collapsed;
        }

        private static IEditableCollectionView GetEditableView(DependencyObject reference)
        {
            if (reference != null)
            {
                var itemsControl = reference.FindAncestor<TreeViewItem>().FindAncestor<ItemsControl>();
                if (itemsControl != null)
                {
                    return itemsControl.Items;
                }
            }

            return null;
        }

        #region Drag

        private Point startPoint;

        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            startPoint = e.GetPosition(null);
        }

        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var mousePos = e.GetPosition(null);
                var diff = startPoint - mousePos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance
                    || Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    var treeView = sender as TreeView;
                    var treeViewItem =
                        FindAncestor<TreeViewItem>((DependencyObject)e.OriginalSource);

                    if (treeView == null || treeViewItem == null)
                        return;

                    var viewModel = treeView.SelectedItem as IProductElementViewModel;
                    if (viewModel == null)
                        return;

                    var dragData = new DataObject("VSPAT", viewModel.Data);
                    DragDrop.DoDragDrop(treeViewItem, dragData, DragDropEffects.Move);
                }
            }
        }

        #endregion

        #region Drop

        internal event DragEventHandler ElementDragEnter;
        internal event DragEventHandler ElementDragLeave;
        internal event DragEventHandler ElementDrop;

        internal DragDropEffects effects = DragDropEffects.None;

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;

            if (ElementDragEnter != null)
            {
                ElementDragEnter(((IProductElementViewModel)(((TreeViewItem)sender).DataContext)).Data, e);
            }

            effects = e.Effects;
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            e.Effects = effects;
            e.Handled = true;

            if (ElementDragEnter != null)
            {
                ElementDragEnter(((IProductElementViewModel)(((TreeViewItem)sender).DataContext)).Data, e);
            }
        }

        private void OnDragLeave(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;

            if (ElementDragLeave != null)
            {
                ElementDragLeave(((IProductElementViewModel)(((TreeViewItem)sender).DataContext)).Data, e);
            }
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            if (ElementDrop != null)
            {
                ElementDrop(((IProductElementViewModel)(((TreeViewItem)sender).DataContext)).Data, e);
            }
            e.Handled = true;
        }

        #endregion

        private void OnItemMouseRightButtonDown(object sender, MouseEventArgs e)
        {
            var item = sender as TreeViewItem;
            if (item != null)
            {
                item.Focus();
                e.Handled = true;
            }
        }

        private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var item = sender as TreeViewItem;

            if (item != null)
            {
                var type = Type.GetType("NuPattern.Runtime.UI.ViewModels.AutomationMenuOptionViewModel, NuPattern.Runtime.Core");
                var method = type.GetMethod("ReEvaluateCommand", BindingFlags.NonPublic | BindingFlags.Instance);
                if (method != null)
                {
                    foreach (var i in item.ContextMenu.Items)
                    {
                        if (i.GetType() == type)
                        {
                            method.Invoke(i, null);
                        }
                    }
                }
            }
        }

        private void OnItemEditBoxGotFocus(object sender, RoutedEventArgs e)
        {
            var itemEditBox = sender as TextBox;
            if (itemEditBox != null)
            {
                itemEditBox.SelectAll();
                e.Handled = true;
            }
        }

        private void OnItemEditBoxLostFocus(object sender, RoutedEventArgs e)
        {
            var view = GetEditableView(sender as DependencyObject);
            if (view != null && view.IsEditingItem)
            {
                view.CommitEdit();
                e.Handled = true;
            }
        }

        private static T FindAncestor<T>(DependencyObject current)
            where T : DependencyObject
        {
            do
            {
                if (current is T)
                {
                    return (T)current;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            while (current != null);
            return null;
        }

        public event EventHandler SelectedItemChanged;

        private void explorer_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (SelectedItemChanged != null)
            {
                SelectedItemChanged(e.NewValue, EventArgs.Empty);
            }

        }

    }

    public class NSBSHTemplateSelector : DataTemplateSelector
    {
        public DataTemplate NSBSTemplate { get; set; }
        public DataTemplate VSPATTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if ((item as LogicalViewModel.LogicalViewModelNode).InnerViewModel == null)
            {
                return NSBSTemplate;
            }
            else
            {
                return VSPATTemplate;
            }
        }
    }

}
