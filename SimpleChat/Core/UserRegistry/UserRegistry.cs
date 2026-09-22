using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleChat.Core.UserRegistry
{
    public sealed class UserRegistry : IUserRegistry
    {
        private readonly ConcurrentDictionary<string, Guid> _byName = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<Guid, string> _byId = new();
        private int _counter;

        public string GetOrAddName(Guid id)
        {
            if (_byId.TryGetValue(id, out var existing))
                return existing;

            lock (_byId)
            {
                if (_byId.TryGetValue(id, out existing))
                    return existing;

                string name;
                do
                {
                    name = $"User-{Interlocked.Increment(ref _counter)}";
                } while (!_byName.TryAdd(name, id));

                _byId[id] = name;
                return name;
            }
        }

        public Guid GetId(string name) =>
            _byName.TryGetValue(name, out var id)
                ? id
                : throw new KeyNotFoundException($"Имя '{name}' не зарегистрировано.");

        public bool TryGetId(string name, out Guid id) => _byName.TryGetValue(name, out id);
        public bool TryGetName(Guid id, out string name) => _byId.TryGetValue(id, out name!);

        public void Remove(string name)
        {
            if (_byName.TryRemove(name, out var id))
                _byId.TryRemove(id, out _);
        }

        public void Remove(Guid id)
        {
            if (_byId.TryRemove(id, out var name))
                _byName.TryRemove(name, out _);
        }

        public bool TryRename(string oldName, string newName)
        {
            if (!_byName.TryGetValue(oldName, out var id)) return false;
            if (string.Equals(oldName, newName, StringComparison.Ordinal)) return true;

            lock (_byId)
            {
                if (!_byName.TryRemove(oldName, out _)) return false;
                if (!_byName.TryAdd(newName, id))         
                {
                    _byName.TryAdd(oldName, id);
                    return false;
                }
                _byId[id] = newName;
                return true;
            }
        }

        public IReadOnlyCollection<string> Names => _byName.Keys.ToArray();
    }
}
