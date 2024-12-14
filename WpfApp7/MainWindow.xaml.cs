using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System;
using System.Collections.ObjectModel;
using System.IO;


    //TODO: сделать файловый менеджер
    //TODO: сделать кнопку на примере диске 
    //TODO: с помощью двойного щелчка открывается эта залупень
    //TODO: была работа с содержимым в данном проводнике 
    //TODO: выводилось содержимое корня 
    //TODO: добавить добавление вкладок в проводнике как в 11 винде 
namespace WpfApp7;

public partial class MainWindow : Window
{ 
    private ObservableCollection<FileSystemInfo> _files;
    private string _currentPath;
    private Stack<string> _back = new Stack<string>();
    private Stack<string> _forward = new Stack<string>();
    public MainWindow()
    {
        InitializeComponent();
        InitializeFileManager();
    }
        private void InitializeFileManager()
        {
            _files = new ObservableCollection<FileSystemInfo>();
            FileListView.ItemsSource = _files;

            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady)
                {
                    var item = new TreeViewItem
                    {
                        Header = drive.Name,
                        Tag = drive.RootDirectory
                    };
                    FolderTreeView.Items.Add(item);
                }
            }

            FolderTreeView.SelectedItemChanged += (s, e) =>
            {
                var item = FolderTreeView.SelectedItem as TreeViewItem;
                if (item?.Tag is DirectoryInfo dir)
                {
                    _currentPath = dir.FullName;
                    LoadFiles(_currentPath);
                }
            };

            FileListView.MouseDoubleClick += (s, e) =>
            {
                OpenSelectedItem();
            };
        }

        private void OpenSelectedItem()
    {
        var selected = FileListView.SelectedItem as FileSystemInfo;
        if (selected == null) return;

        try
        {
            if (!selected.Exists)
            {
                MessageBox.Show("Файл или папка не существует.");
                LoadFiles(_currentPath);
                return;
            }

            if (selected is DirectoryInfo)
            {
                if (!Directory.Exists(selected.FullName))
                {
                    MessageBox.Show("Папка не существует.");
                    LoadFiles(_currentPath);
                    return;
                }
                _currentPath = selected.FullName;
                LoadFiles(_currentPath);
            }
            else
            {
                if (!File.Exists(selected.FullName))
                {
                    MessageBox.Show("Файл не существует.");
                    LoadFiles(_currentPath);
                    return;
                }
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = selected.FullName,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(startInfo);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при открытии: {ex.Message}");
        }
    }

        private void LoadFiles(string path, bool addToHistory = true)
    {
        if (string.IsNullOrEmpty(path))
        {
            MessageBox.Show("Указан некорректный путь");
            return;
        }

        if (addToHistory && _currentPath != null)
        {
            _back.Push(_currentPath);
            _forward.Clear();
        }

        try
        {
            _currentPath = path;
            _files.Clear();
        
            var dir = new DirectoryInfo(path);
            if (!dir.Exists)
            {
                MessageBox.Show("Указанная директория не существует");
                return;
            }

            var parentDir = new DirectoryInfo(Path.Combine(path, ".."));
            if (parentDir.FullName != dir.FullName) // Проверка на корневую директорию
            {
                _files.Add(parentDir);
            }

            foreach (var directory in dir.GetDirectories())
            {
                _files.Add(directory);
            }
            foreach (var file in dir.GetFiles())
            {
                _files.Add(file);
            }
        }
        catch (UnauthorizedAccessException)
        {
            MessageBox.Show("Отказано в доступе к директории");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при загрузке файлов: {ex.Message}");
        }
    }

        private void OpenItem_Click(object sender, RoutedEventArgs e)
        {
            OpenSelectedItem();
        }

        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            var selected = FileListView.SelectedItem as FileSystemInfo;
            if (selected == null) return;

            try
            {
                if (!selected.Exists)
                {
                    MessageBox.Show("Файл или папка уже не существует.");
                    LoadFiles(_currentPath);
                    return;
                }

                var message = selected is DirectoryInfo ? 
                    $"Вы уверены, что хотите удалить папку '{selected.Name}' и все её содержимое?" :
                    $"Вы уверены, что хотите удалить файл '{selected.Name}'?";

                if (MessageBox.Show(message, "Подтверждение", 
                        MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    if (selected is DirectoryInfo dir)
                    {
                        Directory.Delete(dir.FullName, true);
                    }
                    else
                    {
                        File.Delete(selected.FullName);
                    }
                    LoadFiles(_currentPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении: {ex.Message}");
            }
        }

        private void CreateFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InputDialog("Введите имя папки");
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var path = Path.Combine(_currentPath, dialog.ResponseText);
                    Directory.CreateDirectory(path);
                    LoadFiles(_currentPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при создании папки: {ex.Message}");
                }
            }
        }

        private void CreateFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InputDialog("Введите имя файла");
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var path = Path.Combine(_currentPath, dialog.ResponseText);
                    if (!File.Exists(path))
                    {
                        File.Create(path).Dispose(); 
                        LoadFiles(_currentPath);
                    }
                    else
                    {
                        MessageBox.Show("Файл с таким именем уже существует.");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при создании файла: {ex.Message}");
                }
            }
        }
        
        private void NavigateBack()
        {
            if (_back.Count > 0)
            {
                _forward.Push(_currentPath);
                var previousPath = _back.Pop();
                LoadFiles(previousPath, false);
            }
        }

        private void NavigateForward()
        {
            if (_forward.Count > 0)
            {
                _back.Push(_currentPath);
                var nextPath = _forward.Pop();
                LoadFiles(nextPath, false);
            }
        }
        
        private void ButtonBack_Click(object sender, RoutedEventArgs e)
        {
            NavigateBack();
        }

        private void ButtonForward_Click(object sender, RoutedEventArgs e)
        {
            NavigateForward();
        }

        
}

