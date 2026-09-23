using System.Collections.Concurrent;
using SimpleChat.Core.UserRegistry;

namespace Testing.CoreTesting
{
    public class UserRegistryTests
    {
        // ---------- GetOrAddName ----------

        [Fact]
        public void GetOrAddName_NewId_GeneratesNameWithPrefix()
        {
            var registry = new UserRegistry();

            string name = registry.GetOrAddName(Guid.NewGuid());

            Assert.StartsWith("User-", name);
        }

        [Fact]
        public void GetOrAddName_SameId_IsIdempotent()
        {
            var registry = new UserRegistry();
            var id = Guid.NewGuid();

            var first = registry.GetOrAddName(id);
            var second = registry.GetOrAddName(id);

            Assert.Equal(first, second);
            Assert.Single(registry.Names);
        }

        [Fact]
        public void GetOrAddName_MultipleIds_GeneratesDistinctNames()
        {
            var registry = new UserRegistry();

            var names = Enumerable.Range(0, 50)
                .Select(_ => registry.GetOrAddName(Guid.NewGuid()))
                .ToArray();

            Assert.Equal(names.Length, names.Distinct().Count());
        }

        [Fact]
        public void GetOrAddName_ConcurrentCalls_ReturnSameName()
        {
            var registry = new UserRegistry();
            var id = Guid.NewGuid();
            var bag = new ConcurrentBag<string>();

            Parallel.For(0, 200, _ => bag.Add(registry.GetOrAddName(id)));

            Assert.Single(bag.Distinct());
        }

        // ---------- GetId / TryGet ----------

        [Fact]
        public void GetId_RegisteredName_ReturnsId()
        {
            var registry = new UserRegistry();
            var id = Guid.NewGuid();
            string name = registry.GetOrAddName(id);

            Assert.Equal(id, registry.GetId(name));
        }

        [Fact]
        public void GetId_UnknownName_ThrowsKeyNotFound()
        {
            var registry = new UserRegistry();

            var ex = Assert.Throws<KeyNotFoundException>(() => registry.GetId("ghost"));
            Assert.Contains("ghost", ex.Message);
        }

        [Fact]
        public void TryGetId_And_TryGetName_BehaveAsExpected()
        {
            var registry = new UserRegistry();
            var id = Guid.NewGuid();
            string name = registry.GetOrAddName(id);

            Assert.True(registry.TryGetId(name, out var gotId));
            Assert.Equal(id, gotId);
            Assert.False(registry.TryGetId("nope", out _));

            Assert.True(registry.TryGetName(id, out var gotName));
            Assert.Equal(name, gotName);
            Assert.False(registry.TryGetName(Guid.NewGuid(), out _));
        }

        // ---------- Remove ----------

        [Fact]
        public void Remove_ByName_RemovesBothDirections()
        {
            var registry = new UserRegistry();
            var id = Guid.NewGuid();
            string name = registry.GetOrAddName(id);

            registry.Remove(name);

            Assert.False(registry.TryGetId(name, out _));
            Assert.False(registry.TryGetName(id, out _));
        }

        [Fact]
        public void Remove_ById_RemovesBothDirections()
        {
            var registry = new UserRegistry();
            var id = Guid.NewGuid();
            string name = registry.GetOrAddName(id);

            registry.Remove(id);

            Assert.False(registry.TryGetId(name, out _));
            Assert.False(registry.TryGetName(id, out _));
        }

        // ---------- TryRename ----------

        [Fact]
        public void TryRename_ValidNames_UpdatesBothDirections()
        {
            var registry = new UserRegistry();
            var id = Guid.NewGuid();
            string oldName = registry.GetOrAddName(id);

            Assert.True(registry.TryRename(oldName, "Alice"));
            Assert.Equal(id, registry.GetId("Alice"));
            Assert.True(registry.TryGetName(id, out var name));
            Assert.Equal("Alice", name);
        }

        [Fact]
        public void TryRename_TargetTaken_RollsBackAndKeepsBoth()
        {
            var registry = new UserRegistry();
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            string nameA = registry.GetOrAddName(a);
            string nameB = registry.GetOrAddName(b);

            Assert.False(registry.TryRename(nameA, nameB));

            Assert.Equal(a, registry.GetId(nameA));
            Assert.Equal(b, registry.GetId(nameB));
        }

        [Fact]
        public void TryRename_UnknownOldName_ReturnsFalse()
        {
            var registry = new UserRegistry();
            Assert.False(registry.TryRename("ghost", "new"));
        }
    }
}
