using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Threading.Tasks;

namespace WorkSchedulerApp.Views;

public partial class TextInputDialog : Window
{
    private TaskCompletionSource<string?> _tcs = new();

    public TextInputDialog()
    {
        InitializeComponent();
    }

    public void Init(string title, string prompt, string? defaultText)
    {
        Title = title;
        PromptText.Text = prompt;
        InputBox.Text = defaultText ?? "";
    }

    public Task<string?> ShowDialogAsync(Window owner)
    {
        this.ShowDialog(owner);
        return _tcs.Task;
    }

    private void Ok_Click(object? sender, RoutedEventArgs e)
    {
        _tcs.TrySetResult(InputBox.Text);
        Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        _tcs.TrySetResult(null);
        Close();
    }
}