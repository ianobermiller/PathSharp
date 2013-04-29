using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Permissions;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
        private bool isElevated = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        private ObservableCollection<string> paths = new ObservableCollection<string>();
        private Stack<string[]> history = new Stack<string[]>();
        private Stack<string[]> future = new Stack<string[]>();
        private EnvironmentVariableTarget target = EnvironmentVariableTarget.User;

        public string MoveToOtherContextMenuHeader { get; set; }
        public bool IsElevated { get { return isElevated; } }
        public bool IsNotElevated { get { return !isElevated; } }

        public MainWindow()
        {
            this.MoveToOtherContextMenuHeader = "Move to Machine";
            
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

            GongSolutions.Wpf.DragDrop.DragDrop.SetIsDragSource(uxPaths, true);
            GongSolutions.Wpf.DragDrop.DragDrop.SetIsDropTarget(uxPaths, true);
            GongSolutions.Wpf.DragDrop.DragDrop.SetDropHandler(uxPaths, this);
        }

        private void OnAddClicked(object sender, RoutedEventArgs e)
        {
            var dialog = new VistaFolderBrowserDialog();
            dialog.Description = "Select a directory to add to the path";
            dialog.UseDescriptionForTitle = true;
            if (dialog.ShowDialog() == true)
            {
                AddPath(dialog.SelectedPath);
            }
        }

        private void AddPath(string path)
        {
            SaveHistory();
            paths.Add(path);
            SavePath();
            uxPaths.ScrollIntoView(uxPaths.Items.GetItemAt(uxPaths.Items.Count - 1));
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

            SaveHistory();
            var dataObject = dropInfo.Data as DataObject;
            if (from < 0 && dataObject != null)
            {
                // External drag and drop
                var droppedFilePaths = dataObject.GetData(DataFormats.FileDrop, true) as string[];
                if (droppedFilePaths != null)
                {
                    var dirs = droppedFilePaths.Where(f => Directory.Exists(f));
                    foreach (var dir in dirs)
                    {
                        paths.Insert(to, dir);
                        to++;
                    }
                }
            }
            else
            {
                if (from < to)
                {
                    to--;
                }

                paths.Move(from, to);
            }
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
            Task.Factory.StartNew(() => Environment.SetEnvironmentVariable("PATH", combined, target));
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
            File.WriteAllText("path-backup-" + target + "-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".txt", env);
        }

        private string GetPathEnv()
        {
            return Environment.GetEnvironmentVariable("PATH", target);
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            var droppedFilePaths = e.Data.GetData(DataFormats.FileDrop, true) as string[];
            var dirs = droppedFilePaths.Where(f => Directory.Exists(f));
            foreach (var dir in dirs)
            {
                AddPath(dir);
            }
        }

        private void uxNewPathKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                var dir = uxNewPath.Text;
                if (Directory.Exists(dir))
                {
                    AddPath(dir);
                    uxNewPath.Text = "";
                }
            }
        }

        private void uxNewPathPopulating(object sender, System.Windows.Controls.PopulatingEventArgs e)
        {
            string text = uxNewPath.Text;

            if (text.Length <= 0) return;

            string dirname = Path.GetDirectoryName(text);
            if (Directory.Exists(dirname))
            {
                var candidates = Directory
                    .GetDirectories(dirname, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(d => d.StartsWith(dirname, StringComparison.InvariantCultureIgnoreCase)).ToArray();
                uxNewPath.ItemsSource = candidates;
                uxNewPath.PopulateComplete();
            }
        }

        private void uxTabsSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            this.target = uxTabs.SelectedIndex == 0 ? EnvironmentVariableTarget.User : EnvironmentVariableTarget.Machine;
            this.MoveToOtherContextMenuHeader = uxTabs.SelectedIndex == 0 ? "Move to Machine" : "Move to User";
            LoadPath();
        }

        private void uxRestartAsAdminClick(object sender, RoutedEventArgs e)
        {
            var exe = Assembly.GetEntryAssembly().Location;
            Process.Start(new ProcessStartInfo()
            {
                Verb = "runas",
                FileName = exe
            });
            this.Close();
        }

        private void MoveItemToOtherTabClick(object sender, RoutedEventArgs e)
        {
            // Remove from this view
            var index = uxPaths.SelectedIndex;
            if (index < 0) return;
            var path = paths[index];
            Delete();

            // Switch views
            uxTabs.SelectedIndex = uxTabs.SelectedIndex == 0 ? 1 : 0;

            // Add to this view
            AddPath(path);
        }
    }
}
