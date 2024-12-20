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
using Microsoft.VisualBasic.FileIO; //это для FileSystem

//TODO: Реализовать файловый менеджер
//TODO: Добавить кнопку для работы с дисками
//TODO: Реализовать открытие по двойному щелчку
//TODO: Добавить работу с содержимым в проводнике
//TODO: Реализовать вывод содержимого корневого каталога
//TODO: Добавить поддержку вкладок как в Windows 11
//TODO: Добавить строку для отображения текущего пути
//TODO: Создать не существующее пространство и организовать с ним работу
//TODO: Убрать двойной клик с шага назад 
//TODO: Контекстное меню для папки и файла для файлов и папок то есть: удаление, копирование, вырезание, вставка, открыть в терминале, переименовать  
//TODO: СДЕЛАТЬ ЕБУЧЕЕ КОЛЕСИКО
//TODO: сделать удаление в корзину без подтверждения и безвозратное с подтверждением

namespace WpfApp7
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<FileSystemInfo> _files;
        private string _currentPath;
        private Stack<string> _back = new Stack<string>();
        private Stack<string> _forward = new Stack<string>();
        private string _clipboardPath;
        private bool _isCut;

        /// <summary>
        /// Конструктор класса MainWindow. Инициализирует компоненты и загружает начальные данные.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            _files = new ObservableCollection<FileSystemInfo>();
            FileListView.ItemsSource = _files;
            LoadFolderTreeView();
            LoadFiles(Environment.GetFolderPath(Environment.SpecialFolder.Desktop)); // Начальная директория
            FileListView.ContextMenu = CreateContextMenu();
        }

        /// <summary>
        /// Открывает выбранный файл или директорию. Если это файл, он запускается, если директория - загружается её содержимое.
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

        /// <summary>
        /// Загружает файлы и директории из указанного пути и обновляет интерфейс.
        /// </summary>
        /// <param name="path">Путь к директории для загрузки.</param>
        /// <param name="addToHistory">Флаг, указывающий, нужно ли добавлять путь в историю навигации.</param>
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

        /// <summary>
        /// Обработчик события нажатия на элемент "Открыть" в контекстном меню.
        /// </summary>
        private void OpenItem_Click(object sender, RoutedEventArgs e)
        {
            OpenSelectedItem();
        }

        /// <summary>
        /// Удаляет выбранный файл или директорию после подтверждения пользователя.
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

        /// <summary>
        /// Создает новую папку в текущей директории после ввода имени пользователем.
        /// </summary>
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

        /// <summary>
        /// Создает новый файл в текущей директории после ввода имени и расширения пользователем.
        /// </summary>
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
                        {
                        } // Создаем файл и сразу закрываем поток

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

        /// <summary>
        /// Переходит к предыдущей директории в истории навигации.
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
        /// Переходит к следующей директории в истории навигации.
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
        /// Обработчик события нажатия кнопки "Назад". Осуществляет переход к предыдущей директории.
        /// </summary>
        private void ButtonBack_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null)
            {
                button.IsEnabled = false; // Disable the button
                NavigateBack();
                button.IsEnabled = true; // Re-enable the button
            }
        }

        /// <summary>
        /// Обработчик события нажатия кнопки "Вперед". Осуществляет переход к следующей директории.
        /// </summary>
        private void ButtonForward_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null)
            {
                button.IsEnabled = false; // Disable the button
                NavigateForward();
                button.IsEnabled = true; // Re-enable the button
            }
        }

        /// <summary>
        /// Загружает дерево папок в TreeView, добавляя доступные диски.
        /// </summary>
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

        /// <summary>
        /// Обработчик события разворачивания элемента в TreeView. Загружает подкаталоги выбранного элемента.
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
                        if ((subDir.Attributes & FileAttributes.Hidden) == 0) // Пропуск скрытых папок
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
        /// Обработчик события изменения выбранного элемента в TreeView. Загружает файлы выбранной директории.
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
        /// Обработчик события двойного щелчка мыши по элементу в списке файлов. Открывает выбранный элемент.
        /// </summary>
        private void FileListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                OpenSelectedItem();
            }
        }

        /// <summary>
        /// Обработчик события нажатия клавиши в списке файлов. Удаляет выбранный элемент при нажатии Delete.
        /// </summary>
        private void FileListView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                if (Keyboard.Modifiers == ModifierKeys.Shift)
                    DeleteSelectedItem();
                else
                    DeleteToRecycleBin();
            }
        }

        /// <summary>
        /// Удаляет выбранный элемент без возможности восстановления.
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
                    DeleteToRecycleBin();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении: {ex.Message}");
            }
        }

        /// <summary>
        /// Создает контекстное меню для списка файлов с различными действиями.
        /// </summary>
        /// <returns>Возвращает созданное контекстное меню.</returns>
        private ContextMenu CreateContextMenu()
        {
            var contextMenu = new ContextMenu();

            var menuItemOpen = new MenuItem { Header = "Открыть" };
            menuItemOpen.Click += (s, e) => OpenSelectedItem();

            var menuItemCopy = new MenuItem { Header = "Копировать" };
            menuItemCopy.Click += (s, e) => CopySelectedItem();

            var menuItemCut = new MenuItem { Header = "Вырезать" };
            menuItemCut.Click += (s, e) => CutSelectedItem();

            var menuItemPaste = new MenuItem { Header = "Вставить" };
            menuItemPaste.Click += (s, e) => PasteItem();

            var menuItemDelete = new MenuItem { Header = "Удалить" };
            menuItemDelete.Click += (s, e) => DeleteSelectedItem();

            var menuItemRename = new MenuItem { Header = "Переименовать" };
            menuItemRename.Click += (s, e) => RenameSelectedItem();

            var menuItemOpenInTerminal = new MenuItem { Header = "Открыть в терминале" };
            menuItemOpenInTerminal.Click += (s, e) => OpenInTerminal();

            contextMenu.Items.Add(menuItemOpen);
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
        /// Переименовывает выбранный элемент после ввода нового имени пользователем.
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
        /// Открывает командную строку в выбранной директории.
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
        /// Вырезает выбранный элемент для последующего перемещения.
        /// </summary>
        private void CutSelectedItem()
        {
            var selected = FileListView.SelectedItem as FileSystemInfo;
            if (selected == null) return;

            _clipboardPath = selected.FullName;
            _isCut = true;
        }

        /// <summary>
        /// Вставляет элемент из буфера обмена в текущую директорию.
        /// </summary>
        private void PasteItem()
        {
            if (string.IsNullOrEmpty(_clipboardPath)) return;

            try
            {
                string fileName = Path.GetFileName(_clipboardPath);
                string destinationPath = Path.Combine(_currentPath, fileName);

                // Проверка существования целевого файла
                if (File.Exists(destinationPath) || Directory.Exists(destinationPath))
                {
                    if (MessageBox.Show("Файл с таким именем уже существует. Заменить?",
                            "Подтверждение", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                        return;
                }

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
                    _clipboardPath = null;

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
        /// Рекурсивно копирует директорию и её содержимое в указанное место.
        /// </summary>
        /// <param name="sourcePath">Исходный путь директории.</param>
        /// <param name="destinationPath">Целевой путь директории.</param>
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
        /// Перемещает выбранный элемент в корзину.
        /// </summary>
        private void DeleteToRecycleBin()
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
                    ? $"Вы уверены, что хотите переместить папку '{selected.Name}' в корзину?"
                    : $"Вы уверены, что хотите переместить файл '{selected.Name}' в корзину?";

                if (MessageBox.Show(message, "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) ==
                    MessageBoxResult.Yes)
                {
                    if (selected is DirectoryInfo dir)
                    {
                        FileSystem.DeleteDirectory(dir.FullName, UIOption.OnlyErrorDialogs,
                            RecycleOption.SendToRecycleBin);
                    }
                    else
                    {
                        FileSystem.DeleteFile(selected.FullName, UIOption.OnlyErrorDialogs,
                            RecycleOption.SendToRecycleBin);
                    }

                    LoadFiles(_currentPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении в корзину: {ex.Message}");
            }
        }
    }
}