using System;
using System.Globalization;
using System.Windows.Input;
using AutoKassa.Helpers;

namespace AutoKassa.ViewModels
{
    /// <summary>
    /// Изолированный калькулятор для ввода суммы.
    /// Состояние и команды калькулятора были вынесены из TransactionEditViewModel
    /// для уменьшения связности и упрощения тестирования.
    /// </summary>
    public class CalculatorViewModel : ViewModelBase
    {
        private bool _isOpen;
        private string _display = "";
        private string _currentInput = "";
        private decimal _left;
        private string _op;
        private bool _waiting;

        /// <summary>Вызывается, когда пользователь подтверждает результат (=, ✓).</summary>
        public Action<string> OnResult { get; set; }

        public CalculatorViewModel()
        {
            ToggleCommand    = new RelayCommand(_ => { IsOpen = !IsOpen; if (IsOpen) Clear(); });
            DigitCommand     = new RelayCommand(p => { if (p is string d) Digit(d); });
            OpCommand        = new RelayCommand(p => { if (p is string op) Operator(op); });
            EqualsCommand    = new RelayCommand(_ => Equals_());
            ClearCommand     = new RelayCommand(_ => Clear());
            BackspaceCommand = new RelayCommand(_ => Backspace());
        }

        public bool IsOpen
        {
            get => _isOpen;
            set => SetProperty(ref _isOpen, value);
        }

        public string Display
        {
            get => _display;
            set => SetProperty(ref _display, value);
        }

        public ICommand ToggleCommand { get; }
        public ICommand DigitCommand { get; }
        public ICommand OpCommand { get; }
        public ICommand EqualsCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand BackspaceCommand { get; }

        public void Clear()
        {
            _currentInput = "";
            _left = 0;
            _op = null;
            _waiting = false;
            Display = "";
        }

        private void Digit(string digit)
        {
            if (_waiting)
            {
                _currentInput = digit == "." ? "0." : digit;
                _waiting = false;
            }
            else
            {
                if (digit == "." && _currentInput.Contains('.')) return;
                if (_currentInput == "0" && digit != ".")
                    _currentInput = digit;
                else
                    _currentInput += digit;
            }
            UpdateDisplay();
        }

        private void Operator(string op)
        {
            if (decimal.TryParse(_currentInput.Replace(',', '.'),
                NumberStyles.Any, CultureInfo.InvariantCulture, out decimal val))
            {
                if (_op != null && !_waiting)
                    _left = Compute(_left, val, _op);
                else
                    _left = val;
            }
            _op = op;
            _waiting = true;
            UpdateDisplay();
        }

        private void Equals_()
        {
            if (_op == null)
            {
                if (decimal.TryParse(_currentInput.Replace(',', '.'),
                    NumberStyles.Any, CultureInfo.InvariantCulture, out decimal v))
                {
                    OnResult?.Invoke(v.ToString(CultureInfo.InvariantCulture));
                    IsOpen = false;
                }
                return;
            }

            if (!decimal.TryParse(_currentInput.Replace(',', '.'),
                NumberStyles.Any, CultureInfo.InvariantCulture, out decimal right))
                return;

            var result = Compute(_left, right, _op);
            Display = $"{_left} {_op} {right} = {result}";
            OnResult?.Invoke(result.ToString(CultureInfo.InvariantCulture));
            _left = result;
            _op = null;
            _currentInput = result.ToString(CultureInfo.InvariantCulture);
            _waiting = false;
            IsOpen = false;
        }

        private void Backspace()
        {
            if (_currentInput.Length > 0)
            {
                _currentInput = _currentInput[..^1];
                UpdateDisplay();
            }
        }

        private void UpdateDisplay()
        {
            if (_op == null)
                Display = _currentInput;
            else if (_waiting)
                Display = $"{_left} {_op}";
            else
                Display = $"{_left} {_op} {_currentInput}";
        }

        private static decimal Compute(decimal left, decimal right, string op) => op switch
        {
            "+" => left + right,
            "-" => left - right,
            "×" => left * right,
            "÷" => right != 0 ? Math.Round(left / right, 2) : 0,
            _ => right
        };
    }
}
