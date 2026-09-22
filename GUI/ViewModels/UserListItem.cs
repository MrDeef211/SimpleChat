using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GUI.ViewModels
{
    public sealed class UserListItem : INotifyPropertyChanged
    {
        public string Name { get; }

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (_isConnected == value) return;
                _isConnected = value;
                OnPropertyChanged();
            }
        }

        public UserListItem(string name, bool isConnected = false)
        {
            Name = name;
            _isConnected = isConnected;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}