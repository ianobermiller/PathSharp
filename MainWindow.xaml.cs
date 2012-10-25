using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Permissions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using GongSolutions.Wpf.DragDrop;
using MahApps.Metro.Controls;
using Ookii.Dialogs.Wpf;

namespace PathSharp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow, IDropTarget
    {
        private ObservableCollection<string> paths = new ObservableCollection<string>();
        private Stack<string[]> history = new Stack<string[]>();
        private Stack<string[]> future = new Stack<string[]>();

        public MainWindow()
        {
            InitializeComponent();

            var permissions = new EnvironmentPermission(EnvironmentPermissionAccess.AllAccess, "PATH");
            permissions.Demand();

            this.DataContext = this;
            uxPaths.ItemsSource = paths;

            this.AddShortcut(new KeyGesture(Key.Z, ModifierKeys.Control), Undo);
            this.AddShortcut(new KeyGesture(Key.Y, ModifierKeys.Control), Redo);
            uxPaths.AddShortcut(new KeyGesture(Key.Up, ModifierKeys.Control), MoveUp);
            uxPaths.AddShortcut(new KeyGesture(Key.Down, ModifierKeys.Control), MoveDown);
            uxPaths.AddShortcut(new KeyGesture(Key.Delete), Delete);
        }

        private void OnAddClicked(object sender, RoutedEventArgs e)
        {
            var dialog = new VistaFolderBrowserDialog();
            dialog.Description = "Select a directory to add to the path";
            dialog.UseDescriptionForTitle = true;
            if (dialog.ShowDialog() == true)
            {
                SaveHistory();
                paths.Add(dialog.SelectedPath);
                SavePath();
            }
        }

        private void Delete()
        {
            var index = uxPaths.SelectedIndex;
            if (index < 0) return;

            SaveHistory();
            paths.RemoveAt(index);
            SavePath();
        }

        private void MoveUp()
        {
            var index = uxPaths.SelectedIndex;
            if (index < 0) return;

            SaveHistory();
            var newIndex = Math.Max(0, uxPaths.SelectedIndex - 1);
            paths.Move(index, newIndex);
            uxPaths.ScrollIntoView(paths[newIndex]);
            SavePath();
        }

        private void MoveDown()
        {
            var index = uxPaths.SelectedIndex;
            if (index < 0) return;

            SaveHistory();
            var newIndex = Math.Min(index + 1, paths.Count - 1);
            paths.Move(index, newIndex);
            uxPaths.ScrollIntoView(paths[newIndex]);
            SavePath();
        }

        private void SaveHistory()
        {
            history.Push(paths.ToArray());
            future.Clear();
        }

        private void Undo()
        {
            if (!history.Any()) return;

            future.Push(paths.ToArray());
            var newPaths = history.Pop();

            SetPaths(newPaths);
            SavePath();
        }

        private void Redo()
        {
            if (!future.Any()) return;

            var newPaths = future.Pop();

            history.Push(paths.ToArray());
            SetPaths(newPaths);
            SavePath();
        }

        public new void DragOver(DropInfo dropInfo)
        {
            dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
            dropInfo.Effects = DragDropEffects.Move;
        }

        public new void Drop(DropInfo dropInfo)
        {
            var from = paths.IndexOf(dropInfo.Data as string);
            var to = dropInfo.InsertIndex;

            if (from < to)
            {
                to--;
            }

            SaveHistory();
            paths.Move(from, to);
            SavePath();
        }

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            BackupPath();
            LoadPath();
        }

        private void SavePath()
        {
            var combined = string.Join(";", paths);
            Task.Factory.StartNew(() => Environment.SetEnvironmentVariable("PATH", combined, EnvironmentVariableTarget.Machine));
        }

        private void LoadPath()
        {
            var env = GetPathEnv();
            var split = env.Split(';');
            SetPaths(split);
        }

        private void SetPaths(string[] newPaths)
        {
            paths.Clear();
            foreach (var path in newPaths)
            {
                paths.Add(path);
            }
        }

        private void BackupPath()
        {
            var env = GetPathEnv();
            File.WriteAllText("path-backup-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".txt", env);
        }

        private string GetPathEnv()
        {
            return Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine);
        }
    }
}
