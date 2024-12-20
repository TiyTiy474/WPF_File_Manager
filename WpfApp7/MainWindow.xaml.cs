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
using System.Diagnostics;
using System.Windows.Controls.Primitives;
using System.Collections.Generic;
//TODO: Реализовать файловый менеджер
//TODO: Добавить кнопку для работы с дисками
//TODO: Реализовать открытие по двойному щелчку
//TODO: Добавить работу с содержимым в проводнике
//TODO: Реализовать вывод содержимого корневого каталога
//TODO: Добавить поддержку вкладок как в Windows 11
//TODO: Добавить строку для отображения текущего пути
//TODO: Создать не существующее пространство и организовать с ним работу 
namespace WpfApp7
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<FileSystemInfo> _files;
        private string _currentPath;
        private Stack<string> _back = new Stack<string>();
        private Stack<string> _forward = new Stack<string>();

        public MainWindow()
        {
            InitializeComponent();
            _files = new ObservableCollection<FileSystemInfo>();
            FileListView.ItemsSource = _files;
            LoadFolderTreeView();
            LoadFiles(Environment.GetFolderPath(Environment.SpecialFolder.Desktop)); // Начальная директория
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

                PathTextBox.Text = Path.GetDirectoryName(selected.FullName); // Показываем путь к директории
                CurrentFileTextBox.Text = selected.Name; // Показываем только имя файла

                if (selected is DirectoryInfo)
                {
                    _currentPath = selected.FullName;
                    LoadFiles(_currentPath);
                }
                else
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = selected.FullName,
                        UseShellExecute = true
                    };
                    Process.Start(startInfo);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии: {ex.Message}");
            }
        }
        private void LoadFiles(string path, bool addToHistory = true)
        {
            try
            {
                _currentPath = path;
                PathTextBox.Text = path;
                CurrentFileTextBox.Clear();
                _files.Clear();

                var dir = new DirectoryInfo(path);
                if (!dir.Exists)
                {
                    MessageBox.Show("Указанная директория не существует");
                    return;
                }

                var parentDir = dir.Parent;
                if (parentDir != null)
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

                if (addToHistory)
                {
                    _back.Push(path);
                    _forward.Clear();
                }
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("Отказано в доступе к директории");
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

                var message = selected is DirectoryInfo
                    ? $"Вы уверены, что хотите удалить папку '{selected.Name}' и все её содержимое?"
                    : $"Вы уверены, что хотите удалить файл '{selected.Name}'?";
                if (MessageBox.Show(message, "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning) ==
                    MessageBoxResult.Yes)
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
                    if (string.IsNullOrWhiteSpace(dialog.ResponseText))
                    {
                        MessageBox.Show("Имя не может быть пустым.");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при создании папки: {ex.Message}");
                }
            }
        }
        private void CreateFile_Click(object sender, RoutedEventArgs e)
        {

            var dialog = new InputDialog("Введите имя файла с расширением (например: text.txt, doc.docx)");
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var fileName = dialog.ResponseText;
                    // Проверяем, содержит ли имя файла расширение
                    if (string.IsNullOrWhiteSpace(dialog.ResponseText))
                    {
                        MessageBox.Show("Имя не может быть пустым.");
                        return;
                    }

                    if (!fileName.Contains("."))
                    {
                        MessageBox.Show("Пожалуйста, укажите расширение файла (например: .txt, .docx, .pdf)");
                        return;
                    }

                    var path = Path.Combine(_currentPath, fileName);
                    if (!File.Exists(path))
                    {
                        using (File.Create(path))
                        { } // Создаем файл и сразу закрываем поток

                        LoadFiles(_currentPath);
                        //Открываем созданный файл 
                        var startInfo = new ProcessStartInfo
                        {
                            FileName = path,
                            UseShellExecute = true
                        };
                        Process.Start(startInfo);
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
        private void LoadFolderTreeView()
        {
            FolderTreeView.Items.Clear();
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive.IsReady)
                    {
                        var driveItem = new TreeViewItem
                        {
                            Header = $"{drive.Name} ({drive.VolumeLabel})",
                            Tag = drive.RootDirectory
                        };

                        driveItem.Items.Add(new TreeViewItem()); // Placeholder для "+"
                        driveItem.Expanded += FolderTreeView_Expanded;
                        FolderTreeView.Items.Add(driveItem);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при загрузке диска {drive.Name}: {ex.Message}");
                }
            }
        }
        private void FolderTreeView_Expanded(object sender, RoutedEventArgs e)
        {
            var item = e.Source as TreeViewItem;
            if (item?.Tag is DirectoryInfo dir)
            {
                e.Handled = true;
                item.Items.Clear();
                try
                {
                    foreach (var subDir in dir.GetDirectories())
                    {
                        try
                        {
                            var subItem = new TreeViewItem
                            {
                                Header = subDir.Name,
                                Tag = subDir
                            };
                            subItem.Items.Add(new TreeViewItem()); // Placeholder
                            subItem.Expanded += FolderTreeView_Expanded;
                            item.Items.Add(subItem);
                        }
                        catch
                        {
                        } // Пропускаем недоступные папки
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    MessageBox.Show("Отказано в доступе к директории");
                }
            }
        }

        private void FolderTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItem item && item.Tag is DirectoryInfo dir)
            {
                try
                {
                    LoadFiles(dir.FullName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при загрузке директории: {ex.Message}");
                }
            }
        }
        private void FileListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                OpenSelectedItem();
            }
        }
    }
}