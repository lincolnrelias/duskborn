using InventorySystem.Core;
using NUnit.Framework;

namespace InventorySystem.Tests.EditMode
{
    public class InventoryServiceTests
    {
        [Test]
        public void TryAddItem_FillsTopLeftFirst()
        {
            var service = CreateService(3, 2);

            Assert.That(service.TryAddItem(CreateMaterial("a"), out var indexA), Is.True);
            Assert.That(service.TryAddItem(CreateMaterial("b"), out var indexB), Is.True);
            Assert.That(service.TryAddItem(CreateMaterial("c"), out var indexC), Is.True);

            Assert.That(indexA, Is.EqualTo(0));
            Assert.That(indexB, Is.EqualTo(1));
            Assert.That(indexC, Is.EqualTo(2));
        }

        [Test]
        public void TryMoveItem_MovesIntoEmptySlot()
        {
            var service = CreateService(2, 1);
            var item = CreateMaterial("a");
            service.TryAddItem(item, out _);

            var moved = service.TryMoveItem(0, 1);

            Assert.That(moved, Is.True);
            Assert.That(service.GetItem(0), Is.Null);
            Assert.That(service.GetItem(1), Is.SameAs(item));
        }

        [Test]
        public void TryMoveItem_FailsWhenTargetOccupied()
        {
            var service = CreateService(2, 1);
            service.TryAddItem(CreateMaterial("a"), out _);
            service.TryAddItem(CreateMaterial("b"), out _);

            var moved = service.TryMoveItem(0, 1);

            Assert.That(moved, Is.False);
            Assert.That(service.GetItem(0), Is.Not.Null);
            Assert.That(service.GetItem(1), Is.Not.Null);
        }

        [Test]
        public void TrySwapItems_SwapsOccupiedSlots()
        {
            var service = CreateService(2, 1);
            var itemA = CreateMaterial("a");
            var itemB = CreateEquipment("b");
            service.TryAddItem(itemA, out _);
            service.TryAddItem(itemB, out _);

            var swapped = service.TrySwapItems(0, 1);

            Assert.That(swapped, Is.True);
            Assert.That(service.GetItem(0), Is.SameAs(itemB));
            Assert.That(service.GetItem(1), Is.SameAs(itemA));
        }

        [Test]
        public void RemoveItem_ClearsSlot()
        {
            var service = CreateService(1, 1);
            service.TryAddItem(CreateMaterial("a"), out _);

            var removed = service.RemoveItem(0);

            Assert.That(removed, Is.True);
            Assert.That(service.GetItem(0), Is.Null);
        }

        [Test]
        public void TryAddItem_FailsWhenFull()
        {
            var service = CreateService(1, 1);
            service.TryAddItem(CreateMaterial("a"), out _);

            var added = service.TryAddItem(CreateMaterial("b"), out var index);

            Assert.That(added, Is.False);
            Assert.That(index, Is.EqualTo(-1));
        }

        [Test]
        public void AddedItems_AreStoredAtUniqueSlots()
        {
            var service = CreateService(2, 2);
            var a = CreateMaterial("a");
            var b = CreateMaterial("b");
            var c = CreateMaterial("c");

            service.TryAddItem(a, out _);
            service.TryAddItem(b, out _);
            service.TryAddItem(c, out _);

            Assert.That(service.GetItem(0), Is.SameAs(a));
            Assert.That(service.GetItem(1), Is.SameAs(b));
            Assert.That(service.GetItem(2), Is.SameAs(c));
            Assert.That(service.GetItem(3), Is.Null);
        }

        private static InventoryService CreateService(int columns, int rows)
        {
            return new InventoryService(new InventoryGrid(columns, rows));
        }

        private static IInventoryItem CreateMaterial(string id)
        {
            return new MaterialItem(id, $"mat-{id}", "material", "icon_material", "common");
        }

        private static IInventoryItem CreateEquipment(string id)
        {
            return new EquipmentItem(id, $"eq-{id}", "equipment", "icon_equipment", 12);
        }
    }
}
