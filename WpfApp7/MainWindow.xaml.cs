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
using Microsoft.VisualBasic.FileIO; // это для FileSystem и для работы с корзиной
using System.Globalization;
using System.Runtime.InteropServices;
//TODO:горячие клавиши ctrl+c,ctrl+v,ctrl+a,ctrl+shift+n
//TODO:стрелка вверх то есть он возвращеает нас на директорию вверх 
//TODO:доработать вырезание 

namespace WpfApp7
{
    /// <summary>
    /// Главный класс окна приложения.
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Коллекция файлов и директорий в текущем пути.
        /// </summary>
        private ObservableCollection<FileSystemInfo> _files;
        /// <summary>
        /// Текущий путь директории.
        /// </summary>
        private string _currentPath;
        /// <summary>
        /// Стек для управления историей навигации назад.
        /// </summary>
        private Stack<string> _back = new Stack<string>();
        /// <summary>
        /// Стек для управления историей навигации вперед.
        /// </summary>
        private Stack<string> _forward = new Stack<string>();
        /// <summary>
        /// Путь элемента в буфере обмена.
        /// </summary>
        private string _clipboardPath;
        /// <summary>
        /// Указывает, является ли операция в буфере обмена вырезанием.
        /// </summary>
        private bool _isCut;
        /// <summary>
        /// Инициализирует новый экземпляр класса MainWindow.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            _files = new ObservableCollection<FileSystemInfo>();
            FileListView.ItemsSource = _files;
            LoadFolderTreeView();
            LoadFiles(Environment.GetFolderPath(Environment.SpecialFolder.Desktop)); // Начальная директория
            FileListView.ContextMenu = CreateContextMenu();
            this.KeyDown += MainWindow_KeyDown;
            this.Activated += (s, e) => FileListView.Focus();
            
        }

        /// <summary>
        /// Открывает выбранный элемент в списке файлов.
        /// </summary>
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

                PathTextBox.Text = Path.GetDirectoryName(selected.FullName);
                CurrentFileTextBox.Text = selected.Name;

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

        /// <summary>
        /// Загружает файлы и директории из указанного пути.
        /// </summary>
        /// <param name="path">Путь для загрузки файлов.</param>
        /// <param name="addToHistory">Добавлять ли путь в историю навигации.</param>
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

                // Обновляем внешний вид вырезанных элементов
                foreach (var item in _files)
                {
                    var listViewItem = FileListView.ItemContainerGenerator.ContainerFromItem(item) as ListViewItem;
                    if (listViewItem != null)
                    {
                        if (_cutItems.Contains(item.FullName))
                        {
                            listViewItem.Foreground = Brushes.Gray;
                            listViewItem.FontStyle = FontStyles.Italic;
                        }
                        else
                        {
                            listViewItem.Foreground = Brushes.Black;
                            listViewItem.FontStyle = FontStyles.Normal;
                        }
                    }
                }

                if (addToHistory)
                {
                    _back.Push(path);
                    _forward.Clear();
                }

                // Устанавливаем фокус на FileListView
                FileListView.Focus();
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("Отказано в доступе к директории");
            }
        }

        /// <summary>
        /// Обрабатывает событие клика для открытия элемента.
        /// </summary>
        private void OpenItem_Click(object sender, RoutedEventArgs e)
        {
            OpenSelectedItem();
        }

        /// <summary>
        /// Обрабатывает событие клика для удаления элемента.
        /// </summary>
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

                DeleteToRecycleBin(selected);
                LoadFiles(_currentPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении: {ex.Message}");
            }
        }

        /// <summary>
        /// Обрабатывает событие клика для создания новой папки.
        /// </summary>
        private void CreateFolder_Click(object sender, RoutedEventArgs e)
        {
            var newFolderName = "Новая папка";
            var path = Path.Combine(_currentPath, newFolderName);
            path = GetUniqueDirectoryPath(path);

            try
            {
                var directory = Directory.CreateDirectory(path);
                LoadFiles(_currentPath);

                var item = _files.FirstOrDefault(f => f.FullName == directory.FullName);
                if (item != null)
                {
                    FileListView.SelectedItem = item;
                    FileListView.Focus();
                    RenameSelectedItem();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании папки: {ex.Message}");
            }
        }

        /// <summary>
        /// Обрабатывает событие клика для создания нового файла.
        /// </summary>
        private void CreateFile_Click(object sender, RoutedEventArgs e)
        {
            var newFileName = "Новый текстовый документ.txt";
            var path = Path.Combine(_currentPath, newFileName);
            path = GetUniqueFilePath(path);

            try
            {
                using (File.Create(path))
                {
                }

                LoadFiles(_currentPath);

                var item = _files.FirstOrDefault(f => f.FullName == path);
                if (item != null)
                {
                    FileListView.SelectedItem = item;
                    FileListView.Focus();
                    RenameSelectedItem();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании файла: {ex.Message}");
            }
        }

        /// <summary>
        /// Навигация назад по истории.
        /// </summary>
        private void NavigateBack()
        {
            if (_back.Count > 0)
            {
                _forward.Push(_currentPath);
                var previousPath = _back.Pop();
                LoadFiles(previousPath, false);
            }
        }

        /// <summary>
        /// Навигация вперед по истории.
        /// </summary>
        private void NavigateForward()
        {
            if (_forward.Count > 0)
            {
                _back.Push(_currentPath);
                var nextPath = _forward.Pop();
                LoadFiles(nextPath, false);
            }
        }

        /// <summary>
        /// Обрабатывает событие клика для кнопки "Назад".
        /// </summary>
        private void ButtonBack_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null)
            {
                button.IsEnabled = false;
                NavigateBack();
                button.IsEnabled = true;
            }
        }

        /// <summary>
        /// Обрабатывает событие клика для кнопки "Вперед".
        /// </summary>
        private void ButtonForward_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null)
            {
                button.IsEnabled = false;
                NavigateForward();
                button.IsEnabled = true;
            }
        }

        /// <summary>
        /// Загружает дерево папок.
        /// </summary>
        private void LoadFolderTreeView() 
        { 
            FolderTreeView.Items.Clear(); 
            // Добавляем диски
             foreach (var drive in DriveInfo.GetDrives()) 
             { 
                 try 
                 { 
                     if (drive.IsReady) 
                     { 
                         var driveItem = new TreeViewItem 
                         { 
                             Header = $"{drive.Name} ({drive.VolumeLabel})", 
                             Tag = drive.RootDirectory // Убедитесь, что Tag содержит DirectoryInfo
                         }; 
                         driveItem.Items.Add(new TreeViewItem()); 
                         driveItem.Expanded += FolderTreeView_Expanded; 
                         FolderTreeView.Items.Add(driveItem); 
                     } 
                 }
                 catch (Exception ex) 
                 { 
                     MessageBox.Show($"Ошибка при загрузке диска {drive.Name}: {ex.Message}"); 
                 } 
             }
             // Добавляем специальные папки
             AddSpecialFolder(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Рабочий стол"); 
             AddSpecialFolder(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Документы"); 
             AddSpecialFolder(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Изображения"); 
             AddSpecialFolder(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "Музыка"); 
             AddSpecialFolder(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Видео"); 
             AddSpecialFolder(GetDownloadsFolderPath(), "Загрузки");
        }

        /// <summary>
        /// Обрабатывает событие разворачивания элемента в дереве папок.
        /// </summary>
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
                        if ((subDir.Attributes & FileAttributes.Hidden) == 0)
                        {
                            var subItem = new TreeViewItem
                            {
                                Header = subDir.Name,
                                Tag = subDir
                            };
                            subItem.Items.Add(new TreeViewItem());
                            subItem.Expanded += FolderTreeView_Expanded;
                            item.Items.Add(subItem);
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    MessageBox.Show("Отказано в доступе к директории");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при загрузке папок: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Обрабатывает изменение выбранного элемента в дереве папок.
        /// </summary>
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

        /// <summary>
        /// Обрабатывает двойной клик мыши по элементу в списке файлов.
        /// </summary>
        private void FileListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                OpenSelectedItem();
            }
        }

        /// <summary>
        /// Обрабатывает нажатие клавиши в списке файлов.
        /// </summary>
        private void FileListView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                var selected = FileListView.SelectedItem as FileSystemInfo;
                if (selected == null) return;

                if (Keyboard.Modifiers == ModifierKeys.Shift)
                    DeleteSelectedItem();
                else
                    DeleteToRecycleBin(selected); // Передаем выбранный элемент
            }
        }

        /// <summary>
        /// Удаляет выбранный элемент безвозвратно.
        /// </summary>
        private void DeleteSelectedItem()
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
                    ? $"Вы уверены, что хотите безвозвратно удалить папку '{selected.Name}' и все её содержимое?"
                    : $"Вы уверены, что хотите безвозвратно удалить файл '{selected.Name}'?";
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

        /// <summary>
        /// Создает контекстное меню для списка файлов.
        /// </summary>
        private ContextMenu CreateContextMenu()
        {
            var contextMenu = new ContextMenu
            {
                Background = Brushes.White,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1)
            };

            var menuItemOpen = new MenuItem
            {
                Header = "Открыть",
                Icon = new Image
                {
                    Width = 16,
                    Height = 16
                }
            };
            menuItemOpen.Click += OpenItem_Click;

            var menuItemCreateFolder = new MenuItem
            {
                Header = "Создать папку",
                Icon = new Image
                {
                    Width = 16,
                    Height = 16
                }
            };
            menuItemCreateFolder.Click += CreateFolder_Click;

            var menuItemCreateFile = new MenuItem
            {
                Header = "Создать файл",
                Icon = new Image
                {
                    Width = 16,
                    Height = 16
                }
            };
            menuItemCreateFile.Click += CreateFile_Click;

            var menuItemCopy = new MenuItem
            {
                Header = "Копировать",
                Icon = new Image
                {
                    Width = 16,
                    Height = 16
                }
            };
            menuItemCopy.Click += (s, e) => CopySelectedItem();

            var menuItemCut = new MenuItem
            {
                Header = "Вырезать",
                Icon = new Image
                {
                    Width = 16,
                    Height = 16
                }
            };
            menuItemCut.Click += (s, e) => CutSelectedItem();

            var menuItemPaste = new MenuItem
            {
                Header = "Вставить",
                Icon = new Image
                {
                    Width = 16,
                    Height = 16
                }
            };
            menuItemPaste.Click += (s, e) => PasteItem();

            var menuItemDelete = new MenuItem
            {
                Header = "Удалить",
                Icon = new Image
                {
                    Width = 16,
                    Height = 16
                }
            };
            menuItemDelete.Click += (s, e) => DeleteSelectedItem();

            var menuItemRename = new MenuItem
            {
                Header = "Переименовать",
                Icon = new Image
                {
                    Width = 16,
                    Height = 16
                }
            };
            menuItemRename.Click += (s, e) => RenameSelectedItem();

            var menuItemOpenInTerminal = new MenuItem
            {
                Header = "Открыть в терминале",
                Icon = new Image
                {
                    Width = 16,
                    Height = 16
                }
            };
            menuItemOpenInTerminal.Click += (s, e) => OpenInTerminal();

            contextMenu.Items.Add(menuItemOpen);
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(menuItemCreateFolder);
            contextMenu.Items.Add(menuItemCreateFile);
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(menuItemCopy);
            contextMenu.Items.Add(menuItemCut);
            contextMenu.Items.Add(menuItemPaste);
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(menuItemDelete);
            contextMenu.Items.Add(menuItemRename);
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(menuItemOpenInTerminal);

            return contextMenu;
        }

        /// <summary>
        /// Переименовывает выбранный элемент.
        /// </summary>
        private void RenameSelectedItem()
        {
            var selected = FileListView.SelectedItem as FileSystemInfo;
            if (selected == null) return;

            var dialog = new InputDialog("Введите новое имя:");
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    string newPath = Path.Combine(Path.GetDirectoryName(selected.FullName), dialog.ResponseText);
                    if (selected is DirectoryInfo)
                    {
                        Directory.Move(selected.FullName, newPath);
                    }
                    else
                    {
                        File.Move(selected.FullName, newPath);
                    }

                    LoadFiles(_currentPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при переименовании: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Открывает выбранный элемент в терминале.
        /// </summary>
        private void OpenInTerminal()
        {
            var selected = FileListView.SelectedItem as FileSystemInfo;
            string path = selected is DirectoryInfo ? selected.FullName : Path.GetDirectoryName(selected.FullName);

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    WorkingDirectory = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии терминала: {ex.Message}");
            }
        }

        /// <summary>
        /// Копирует выбранный элемент в буфер обмена.
        /// </summary>
        private void CopySelectedItem()
        {
            var selected = FileListView.SelectedItem as FileSystemInfo;
            if (selected == null) return;

            _clipboardPath = selected.FullName;
            _isCut = false;
        }

        /// <summary>
        /// Вырезает выбранный элемент в буфер обмена.
        /// </summary>
        private void CutSelectedItem()
        {
            var selected = FileListView.SelectedItem as FileSystemInfo;
            if (selected == null) return;

            _clipboardPath = selected.FullName;
            _isCut = true;

            // Добавляем элемент в коллекцию вырезанных
            _cutItems.Add(_clipboardPath);

            // Обновляем отображение
            LoadFiles(_currentPath);
        }

        /// <summary>
        /// Вставляет элемент из буфера обмена.
        /// </summary>
        private void PasteItem()
        {
            if (string.IsNullOrEmpty(_clipboardPath)) return;

            try
            {
                string fileName = Path.GetFileName(_clipboardPath);
                string destinationPath = Path.Combine(_currentPath, fileName);

                destinationPath = GetUniqueFilePath(destinationPath);

                if (File.Exists(_clipboardPath))
                {
                    if (_isCut)
                        File.Move(_clipboardPath, destinationPath);
                    else
                        File.Copy(_clipboardPath, destinationPath, true);
                }
                else if (Directory.Exists(_clipboardPath))
                {
                    if (_isCut)
                        Directory.Move(_clipboardPath, destinationPath);
                    else
                        CopyDirectory(_clipboardPath, destinationPath);
                }

                if (_isCut)
                {
                    // Удаляем элемент из коллекции вырезанных
                    _cutItems.Remove(_clipboardPath);
                    _clipboardPath = null;
                }

                LoadFiles(_currentPath);
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("Отказано в доступе. Проверьте права доступа к файлу или папке.");
            }
            catch (IOException ex)
            {
                MessageBox.Show($"Ошибка ввода/вывода: {ex.Message}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при вставке: {ex.Message}");
            }
        }

        /// <summary>
        /// Копирует директорию рекурсивно.
        /// </summary>
        private void CopyDirectory(string sourcePath, string destinationPath)
        {
            Directory.CreateDirectory(destinationPath);

            foreach (string file in Directory.GetFiles(sourcePath))
            {
                string destFile = Path.Combine(destinationPath, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (string dir in Directory.GetDirectories(sourcePath))
            {
                string destDir = Path.Combine(destinationPath, Path.GetFileName(dir));
                CopyDirectory(dir, destDir);
            }
        }

        /// <summary>
        /// Удаляет элемент в корзину.
        /// </summary>
        private void DeleteToRecycleBin(FileSystemInfo selected)
        {
            try
            {
                if (selected is DirectoryInfo dir)
                {
                    FileSystem.DeleteDirectory(dir.FullName, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                }
                else
                {
                    FileSystem.DeleteFile(selected.FullName, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                }

                // Обновляем список файлов после удаления
                LoadFiles(_currentPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении в корзину: {ex.Message}");
            }
        }

        /// <summary>
        /// Получает уникальный путь для файла.
        /// </summary>
        private string GetUniqueFilePath(string path)
        {
            string directory = Path.GetDirectoryName(path);
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);

            int count = 1;
            while (File.Exists(path))
            {
                string tempFileName = $"{fileNameWithoutExtension} ({count++}){extension}";
                path = Path.Combine(directory, tempFileName);
            }

            return path;
        }

        /// <summary>
        /// Получает уникальный путь для директории.
        /// </summary>
        private string GetUniqueDirectoryPath(string path)
        {
            string basePath = path;
            int count = 1;

            while (Directory.Exists(path))
            {
                path = $"{basePath} ({count++})";
            }

            return path;
        }
        
        /// <summary>
        /// Добавляет специальную папку в дерево.
        /// </summary>
        private void AddSpecialFolder(string path, string displayName)
        {
            if (!string.IsNullOrEmpty(path))
            {
                var folderItem = new TreeViewItem
                {
                    Header = displayName,
                    Tag = new DirectoryInfo(path)
                };
                folderItem.Items.Add(new TreeViewItem());
                folderItem.Expanded += FolderTreeView_Expanded;
                FolderTreeView.Items.Add(folderItem);
            }
        } 
        
        /// <summary>
        /// Получает путь к папке загрузок.
        /// </summary>
        private static string GetDownloadsFolderPath()
        {
            IntPtr ppszPath;
            int hr = SHGetKnownFolderPath(new Guid("374DE290-123F-4565-9164-39C4925E467B"), 0, IntPtr.Zero,
                out ppszPath);
            if (hr != 0)
            {
                throw Marshal.GetExceptionForHR(hr);
            }

            string path = Marshal.PtrToStringUni(ppszPath);
            Marshal.FreeCoTaskMem(ppszPath);
            return path;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags,
            IntPtr hToken, out IntPtr ppszPath);
        // Коллекция для хранения путей вырезанных элементов
        private HashSet<string> _cutItems = new HashSet<string>();
        //работа с горячими клавишами
        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.C:
                        CopySelectedItem();
                        break;
                    case Key.V:
                        PasteItem();
                        break;
                    case Key.A:
                        SelectAllItems();
                        break;
                    case Key.N:
                        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                        {
                            CreateFolder_Click(null, null);
                        }
                        break;
                }
            }
            else if (e.Key == Key.Up)
            {
                NavigateToParentDirectory();
            }
        }
    
        private void SelectAllItems()
        {
            FileListView.SelectAll();
        }

        private void NavigateToParentDirectory()
        {
            var currentDir = new DirectoryInfo(_currentPath);
            if (currentDir.Parent != null)
            {
                LoadFiles(currentDir.Parent.FullName);
            }
        }
    }
}