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
            InitializeFileManager();
            InitializeTabSystem();
        }

        private void InitializeTabSystem()
        {
            _tabControl = new TabControl();
            _tabs = new List<TabItem>();

            var newTabButton = new Button
            {
                Content = "+",
                Width = 25,
                Height = 25,
                Margin = new Thickness(5)
            };
            newTabButton.Click += (s, e) => AddNewTab();

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition());

            var topPanel = new DockPanel();
            topPanel.Children.Add(_tabControl);
            topPanel.Children.Add(newTabButton);
            DockPanel.SetDock(newTabButton, Dock.Right);

            Grid.SetRow(topPanel, 0);

            mainGrid.Children.Add(topPanel);

            this.Content = mainGrid;

            AddNewTab();
        }

        private void AddNewTab()
        {
            var newTab = new TabItem();
            var headerPanel = new DockPanel();

            var headerText = new TextBlock
            {
                Text = $"Вкладка {_tabs.Count + 1}",
                Margin = new Thickness(0, 0, 5, 0)
            };

            var closeButton = new Button
            {
                Content = "×",
                Background = null,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(5, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            headerPanel.Children.Add(headerText);
            headerPanel.Children.Add(closeButton);
            newTab.Header = headerPanel;

            var fileManagerGrid = new Grid();
            fileManagerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            fileManagerGrid.RowDefinitions.Add(new RowDefinition());

            var pathBox = new TextBox
            {
                Margin = new Thickness(5),
                IsReadOnly = true
            };
            Grid.SetRow(pathBox, 0);

            var contentGrid = new Grid();
            Grid.SetRow(contentGrid, 1);

            var treeView = new TreeView
            {
                Width = 200,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            var listView = new ListView();

            contentGrid.Children.Add(treeView);
            contentGrid.Children.Add(listView);

            fileManagerGrid.Children.Add(pathBox);
            fileManagerGrid.Children.Add(contentGrid);

            newTab.Content = fileManagerGrid;

            InitializeFileManagerForTab(treeView, listView, pathBox);

            closeButton.Click += (s, e) =>
            {
                if (_tabs.Count > 1)
                {
                    _tabs.Remove(newTab);
                    _tabControl.Items.Remove(newTab);
                }
            };

            _tabs.Add(newTab);
            _tabControl.Items.Add(newTab);
            _tabControl.SelectedItem = newTab;
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

            FileListView.MouseDoubleClick += (s, e) => { OpenSelectedItem(); };
        }

        private void InitializeFileManagerForTab(TreeView treeView, ListView listView, TextBox pathBox)
        {
            try
            {
                foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
                {
                    var item = new TreeViewItem
                    {
                        Header = drive.Name,
                        Tag = drive.RootDirectory
                    };
                    treeView.Items.Add(item);
                }

                treeView.SelectedItemChanged += (s, e) =>
                {
                    try
                    {
                        var item = e.NewValue as TreeViewItem;
                        if (item?.Tag is DirectoryInfo dir)
                        {
                            pathBox.Text = dir.FullName;
                            LoadFilesForTab(listView, dir.FullName);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при загрузке директории: {ex.Message}");
                    }
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при инициализации: {ex.Message}");
            }
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

                CurrentFileTextBox.Text = selected.FullName; // Показываем полный путь к файлу

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
                CurrentFileTextBox.Clear(); // Очищаем поле текущего файла
                _files.Clear();

                var dir = new DirectoryInfo(path);
                if (!dir.Exists)
                {
                    MessageBox.Show("Указанная директория не существует");
                    return;
                }

                // Добавляем переход на уровень выше, если это не корневая директория
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
                    _forward.Clear(); // Очищаем историю вперед при новой навигации
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
    }
}
