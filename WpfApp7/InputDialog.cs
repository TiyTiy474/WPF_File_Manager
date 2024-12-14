using System.Windows;
using System.Windows.Controls;
namespace WpfApp7;

public class InputDialog : Window
{
    private TextBox textBox;
    public string ResponseText { get; private set; }

    public InputDialog(string question)
    {
        Width = 300;
        Height = 150;
        Title = question;
        
        var grid = new Grid();
        textBox = new TextBox { Margin = new Thickness(10) };
        var stackPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var okButton = new Button { Content = "OK", Width = 60, Margin = new Thickness(10) };
        var cancelButton = new Button { Content = "Отмена", Width = 60, Margin = new Thickness(10) };

        okButton.Click += (s, e) => { DialogResult = true; ResponseText = textBox.Text; };
        cancelButton.Click += (s, e) => DialogResult = false;

        stackPanel.Children.Add(okButton);
        stackPanel.Children.Add(cancelButton);

        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        Grid.SetRow(textBox, 0);
        Grid.SetRow(stackPanel, 1);

        grid.Children.Add(textBox);
        grid.Children.Add(stackPanel);

        Content = grid;
    }
}