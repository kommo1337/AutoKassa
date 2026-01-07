using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AutoKassa.Helpers
{
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        /// <summary>
        /// Событие изменения свойства для привязки данных
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Вызов события PropertyChanged для уведомления UI об изменении свойства
        /// </summary>
        /// <param name="propertyName">Имя свойства (заполняется автоматически)</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Установка значения свойства с автоматическим вызовом OnPropertyChanged
        /// </summary>
        /// <typeparam name="T">Тип свойства</typeparam>
        /// <param name="field">Поле для хранения значения</param>
        /// <param name="value">Новое значение</param>
        /// <param name="propertyName">Имя свойства (заполняется автоматически)</param>
        /// <returns>True если значение изменилось, иначе False</returns>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
