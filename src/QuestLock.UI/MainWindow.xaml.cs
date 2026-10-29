using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using QuestLock.Core.Ipc;
using QuestLock.Core.Math;
using QuestLock.Core.Models;
using QuestLock.Core.Storage;
using QuestLock.UI.Native;
using QuestLock.UI.Services;
using Forms = System.Windows.Forms;

namespace QuestLock.UI;

public partial class MainWindow : Window
{
    private readonly StateManager _stateManager = new();
    private readonly ConfigManager _configManager = new();
    private readonly MathGenerator _mathGenerator = new();
    private readonly KeyboardHook _keyboardHook = new();
    private readonly IpcClientService _ipcClient = new();

    private List<MathProblem> _problems = new();
    private int _currentIndex = 0;
    private int _correctCount = 0;
    private int _targetCount = 5;
    private string _currentAnswer = string.Empty;
    private DateTime _questStartTime = DateTime.UtcNow;

    private readonly List<SecondaryMonitorWindow> _secondaryWindows = new();
    private readonly DispatcherTimer _dayCheckTimer = new();
    private bool _isLockActive = false;

    public MainWindow()
    {
        InitializeComponent();

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;

        // Události spánku a probuzení
        SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;

        // Kontrola přechodu půlnoci / nového dne každých 30 sekund
        _dayCheckTimer.Interval = TimeSpan.FromSeconds(30);
        _dayCheckTimer.Tick += DayCheckTimer_Tick;
        _dayCheckTimer.Start();

        // IPC komunikace se systémovou službou
        _ipcClient.OnUnlockGranted += IpcClient_OnUnlockGranted;
        _ipcClient.OnForceLockRequested += IpcClient_OnForceLockRequested;
        _ipcClient.Start();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        SetupScreenPositions();
        CheckAndApplyLockState();
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Okno nikdy nezavíráme natvrdo, pouze skrýváme, aby hlídalo probuzení ze spánku
        e.Cancel = true;
        UnlockAndHide();
    }

    private void SetupScreenPositions()
    {
        // Umístit hlavní okno na primární obrazovku
        var primary = Forms.Screen.PrimaryScreen;
        if (primary != null)
        {
            Left = primary.Bounds.Left;
            Top = primary.Bounds.Top;
            Width = primary.Bounds.Width;
            Height = primary.Bounds.Height;
        }

        // Vytvořit překryvná okna na všech sekundárních monitorech
        foreach (var screen in Forms.Screen.AllScreens)
        {
            if (screen.Primary) continue;

            var secWin = new SecondaryMonitorWindow
            {
                Left = screen.Bounds.Left,
                Top = screen.Bounds.Top,
                Width = screen.Bounds.Width,
                Height = screen.Bounds.Height
            };
            _secondaryWindows.Add(secWin);
        }
    }

    private void CheckAndApplyLockState()
    {
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        var state = _stateManager.Load();

        if (state.IsCompletedToday(today))
        {
            // Dnes již splněno -> zůstáváme tiše na pozadí
            UnlockAndHide();
        }
        else
        {
            // Nový den / nesplněno -> aktivovat zámek
            ShowLock();
        }
    }

    private void ShowLock()
    {
        if (_isLockActive) return;

        var config = _configManager.Load();
        _targetCount = config.Math.TotalQuestions > 0 ? config.Math.TotalQuestions : 5;
        _correctCount = 0;
        _currentIndex = 0;
        _currentAnswer = string.Empty;
        _questStartTime = DateTime.UtcNow;

        _problems = _mathGenerator.GenerateSet(config.Math, _targetCount);

        QuestCard.Visibility = Visibility.Visible;
        SuccessCard.Visibility = Visibility.Collapsed;
        FeedbackText.Text = string.Empty;

        UpdateCurrentQuestionUI();

        // Zobrazit hlavní okno
        Show();
        WindowState = WindowState.Normal;
        Topmost = true;
        Activate();
        Focus();

        // Zobrazit sekundární monitory
        foreach (var sec in _secondaryWindows)
        {
            sec.Show();
            sec.WindowState = WindowState.Normal;
            sec.Topmost = true;
        }

        _keyboardHook.Enable();
        _isLockActive = true;
    }

    private void UnlockAndHide()
    {
        _keyboardHook.Disable();

        foreach (var sec in _secondaryWindows)
        {
            sec.Hide();
        }

        Hide();
        _isLockActive = false;
    }

    private void UpdateCurrentQuestionUI()
    {
        if (_currentIndex < _problems.Count)
        {
            var problem = _problems[_currentIndex];
            ExpressionText.Text = problem.DisplayExpression;
            AnswerText.Text = _currentAnswer;
            ProgressText.Text = $"Úkol {_currentIndex + 1} z {_targetCount}";
            FeedbackText.Text = string.Empty;
        }
    }

    private void AppendDigit(char digit)
    {
        if (_currentAnswer.Length < 6)
        {
            _currentAnswer += digit;
            AnswerText.Text = _currentAnswer;
            FeedbackText.Text = string.Empty;
        }
    }

    private void ClearAnswer()
    {
        _currentAnswer = string.Empty;
        AnswerText.Text = string.Empty;
        FeedbackText.Text = string.Empty;
    }

    private void RemoveLastDigit()
    {
        if (_currentAnswer.Length > 0)
        {
            _currentAnswer = _currentAnswer.Substring(0, _currentAnswer.Length - 1);
            AnswerText.Text = _currentAnswer;
            FeedbackText.Text = string.Empty;
        }
    }

    private void SubmitAnswer()
    {
        if (string.IsNullOrWhiteSpace(_currentAnswer)) return;

        if (!int.TryParse(_currentAnswer, out int userAnswer))
        {
            FeedbackText.Text = "Zadej prosím platné číslo!";
            return;
        }

        var currentProblem = _problems[_currentIndex];
        if (currentProblem.Verify(userAnswer))
        {
            // Správná odpověď
            _correctCount++;
            _currentAnswer = string.Empty;

            if (_correctCount >= _targetCount)
            {
                // Všechny úkoly dokončeny!
                HandleAllCompleted();
            }
            else
            {
                _currentIndex++;
                UpdateCurrentQuestionUI();
            }
        }
        else
        {
            // Chybná odpověď
            FeedbackText.Text = "Výsledek není správný, zkus to znovu!";
            _currentAnswer = string.Empty;
            AnswerText.Text = string.Empty;
        }
    }

    private void HandleAllCompleted()
    {
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        var duration = (int)(DateTime.UtcNow - _questStartTime).TotalSeconds;

        // Uložit lokální stav
        _stateManager.MarkCompleted(today, duration);

        // Odeslat do služby přes IPC
        _ = _ipcClient.SubmitCompletionAsync(_correctCount, _targetCount, duration);

        QuestCard.Visibility = Visibility.Collapsed;
        SuccessCard.Visibility = Visibility.Visible;
    }

    private void DismissSuccess_Click(object sender, RoutedEventArgs e)
    {
        UnlockAndHide();
    }

    #region Klávesnice a NumPad (i bez NumLocku)

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 1. Číslice z hlavní řady klávesnice
        if (e.Key >= Key.D0 && e.Key <= Key.D9)
        {
            char digit = (char)('0' + (e.Key - Key.D0));
            AppendDigit(digit);
            e.Handled = true;
            return;
        }

        // 2. Číslice z NumPadu se zapnutým NumLockem
        if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
        {
            char digit = (char)('0' + (e.Key - Key.NumPad0));
            AppendDigit(digit);
            e.Handled = true;
            return;
        }

        // 3. NumPad při VYPNUTÉM NumLocku (mapování systémových kódů na čísla)
        switch (e.Key)
        {
            case Key.Insert:   // NumPad 0
                AppendDigit('0');
                e.Handled = true;
                return;
            case Key.End:      // NumPad 1
                AppendDigit('1');
                e.Handled = true;
                return;
            case Key.Down:     // NumPad 2
                AppendDigit('2');
                e.Handled = true;
                return;
            case Key.PageDown: // NumPad 3
                AppendDigit('3');
                e.Handled = true;
                return;
            case Key.Left:     // NumPad 4
                AppendDigit('4');
                e.Handled = true;
                return;
            case Key.Clear:    // NumPad 5
                AppendDigit('5');
                e.Handled = true;
                return;
            case Key.Right:    // NumPad 6
                AppendDigit('6');
                e.Handled = true;
                return;
            case Key.Home:     // NumPad 7
                AppendDigit('7');
                e.Handled = true;
                return;
            case Key.Up:       // NumPad 8
                AppendDigit('8');
                e.Handled = true;
                return;
            case Key.PageUp:   // NumPad 9
                AppendDigit('9');
                e.Handled = true;
                return;

            case Key.Back:
            case Key.Delete:
                RemoveLastDigit();
                e.Handled = true;
                return;

            case Key.Enter:
                SubmitAnswer();
                e.Handled = true;
                return;

            case Key.Escape:
                ClearAnswer();
                e.Handled = true;
                return;
        }
    }

    private void PadButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Content is string text && text.Length == 1)
        {
            AppendDigit(text[0]);
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        ClearAnswer();
    }

    private void SubmitButton_Click(object sender, RoutedEventArgs e)
    {
        SubmitAnswer();
    }

    #endregion

    #region Režim spánku a vzdálené odemčení

    private void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            // Probuzení ze spánku -> ověřit, zda je nový den
            Dispatcher.Invoke(CheckAndApplyLockState);
        }
    }

    private void DayCheckTimer_Tick(object? sender, EventArgs e)
    {
        // Pravidelná kontrola pro případ, že PC zůstane zapnuté přes půlnoc
        CheckAndApplyLockState();
    }

    private void IpcClient_OnUnlockGranted(UnlockGrantedPayload payload)
    {
        // Vzdálené odemčení (např. mamka z mobilu přes web)
        Dispatcher.Invoke(() =>
        {
            UnlockAndHide();
        });
    }

    private void IpcClient_OnForceLockRequested()
    {
        // Okamžité studium vyvolané z administrace
        Dispatcher.Invoke(() =>
        {
            _isLockActive = false;
            ShowLock();
        });
    }

    #endregion
}
