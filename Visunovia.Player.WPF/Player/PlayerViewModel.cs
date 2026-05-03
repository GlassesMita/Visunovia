using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Visunovia.Player.WPF.Player;

public class PlayerViewModel : INotifyPropertyChanged
{
    private string _speaker = "";
    private string _dialogueText = "";
    private string? _backgroundPath;
    private bool _hasChoices;
    private bool _isDialogueVisible = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Speaker
    {
        get => _speaker;
        set { _speaker = value; OnPropertyChanged(); }
    }

    public string DialogueText
    {
        get => _dialogueText;
        set { _dialogueText = value; OnPropertyChanged(); }
    }

    public string? BackgroundPath
    {
        get => _backgroundPath;
        set { _backgroundPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasBackground)); }
    }

    public bool HasBackground => !string.IsNullOrEmpty(_backgroundPath);

    public bool HasChoices
    {
        get => _hasChoices;
        set { _hasChoices = value; OnPropertyChanged(); }
    }

    public bool IsDialogueVisible
    {
        get => _isDialogueVisible;
        set { _isDialogueVisible = value; OnPropertyChanged(); }
    }

    public ICommand AdvanceCommand { get; }
    public ICommand ChoiceSelectedCommand { get; }

    public event Action? AdvanceRequested;
    public event Action<int>? ChoiceSelected;

    public PlayerViewModel()
    {
        AdvanceCommand = new RelayCommand(_ => AdvanceRequested?.Invoke());
        ChoiceSelectedCommand = new RelayCommand(param =>
        {
            if (param is int index)
            {
                ChoiceSelected?.Invoke(index);
            }
            else if (param is string indexStr && int.TryParse(indexStr, out var parsed))
            {
                ChoiceSelected?.Invoke(parsed);
            }
        });
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);
}