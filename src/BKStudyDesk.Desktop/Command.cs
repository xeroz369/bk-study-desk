namespace BKStudyDesk.Desktop;

/// <summary>ICommand nhỏ cho phím tắt (Avalonia không có sẵn lớp lệnh đơn giản). Dùng chung cho MainWindow và trang Luyện tập.</summary>
internal sealed class Command(Action run) : System.Windows.Input.ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => run();
}
