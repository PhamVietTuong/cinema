using System.Linq.Expressions;
using Cinema.Business.DTO.Catalog;
using Cinema.Business.Helpers;
using Cinema.Business.Managers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using FluentAssertions;
using Moq;

namespace Cinema.Business.Tests;

public class ComboServiceTests
{
    private readonly Mock<IApplicationUnitOfWork> _uowMock = new();
    private readonly ComboManager _combos;
    private readonly FoodAndDrinkManager _foods;

    private static readonly Guid TheaterA = Guid.NewGuid();
    private static readonly Guid TheaterB = Guid.NewGuid();

    public ComboServiceTests()
    {
        _combos = new ComboManager(_uowMock.Object);
        _foods = new FoodAndDrinkManager(_uowMock.Object);

        _uowMock.Setup(u => u.ComboItemStore.GetCombosUsingAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new List<(Guid ComboId, string ComboName)>());
        _uowMock.Setup(u => u.ComboItemStore.GetByCombosAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new List<ComboItem>());
        _uowMock.Setup(u => u.StockMovementStore.ExistsAsync(It.IsAny<Expression<Func<StockMovement, bool>>>()))
            .ReturnsAsync(false);
    }

    private static FoodAndDrink Food(string name, Guid? theater = null, bool tracked = false, int onHand = 0, bool isCombo = false, bool available = true)
    {
        return new FoodAndDrink
        {
            Id = Guid.NewGuid(),
            Name = name,
            TheaterId = theater ?? TheaterA,
            TrackInventory = tracked,
            QuantityOnHand = onHand,
            IsCombo = isCombo,
            IsAvailable = available
        };
    }

    private void Known(params FoodAndDrink[] items)
    {
        var map = items.ToDictionary(i => i.Id);
        foreach (var item in items)
        {
            _uowMock.Setup(u => u.FoodAndDrinkStore.GetByIdAsync(item.Id)).ReturnsAsync(item);
        }
        _uowMock.Setup(u => u.FoodAndDrinkStore.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids) => ids.Where(map.ContainsKey).ToDictionary(id => id, id => map[id]));
    }

    private static SaveComboRequest Save(FoodAndDrink combo, params (FoodAndDrink Component, int Qty)[] lines)
    {
        return new SaveComboRequest
        {
            ComboId = combo.Id,
            IsCombo = true,
            Components = lines.Select(l => new ComboComponentItem { ComponentId = l.Component.Id, Quantity = l.Qty }).ToList()
        };
    }

    // ── SaveAsync rejections ────────────────────────────────────────────────

    [Fact]
    public async Task Save_UnknownCombo_ThrowsKeyNotFound()
    {
        _uowMock.Setup(u => u.FoodAndDrinkStore.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((FoodAndDrink?)null);

        var act = () => _combos.SaveAsync(new SaveComboRequest { ComboId = Guid.NewGuid(), IsCombo = true });

        await act.Should().ThrowAsync<KeyNotFoundException>();
        _uowMock.Verify(u => u.RollbackTransactionAsync(), Times.Once);
    }

    [Fact]
    public async Task Save_EmptyComponents_Throws()
    {
        var combo = Food("Combo");
        Known(combo);

        var act = () => _combos.SaveAsync(Save(combo));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Save_DuplicateComponents_Throws()
    {
        var combo = Food("Combo");
        var popcorn = Food("Popcorn");
        Known(combo, popcorn);

        var act = () => _combos.SaveAsync(Save(combo, (popcorn, 1), (popcorn, 2)));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Save_SelfComponent_Throws()
    {
        var combo = Food("Combo");
        Known(combo);

        var act = () => _combos.SaveAsync(Save(combo, (combo, 1)));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Save_NestedComboComponent_Throws()
    {
        var combo = Food("Combo");
        var inner = Food("Inner combo", isCombo: true);
        Known(combo, inner);

        var act = () => _combos.SaveAsync(Save(combo, (inner, 1)));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*nested*");
    }

    [Fact]
    public async Task Save_ComponentFromOtherTheater_Throws()
    {
        var combo = Food("Combo");
        var foreign = Food("Cola", theater: TheaterB);
        Known(combo, foreign);

        var act = () => _combos.SaveAsync(Save(combo, (foreign, 1)));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*different theater*");
    }

    [Fact]
    public async Task Save_TrackedCombo_Throws()
    {
        var combo = Food("Combo", tracked: true);
        var popcorn = Food("Popcorn");
        Known(combo, popcorn);

        var act = () => _combos.SaveAsync(Save(combo, (popcorn, 1)));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no stock*");
    }

    [Fact]
    public async Task Save_ComboAlreadyUsedInAnotherCombo_Throws()
    {
        var combo = Food("Combo");
        var popcorn = Food("Popcorn");
        Known(combo, popcorn);
        _uowMock.Setup(u => u.ComboItemStore.GetCombosUsingAsync(combo.Id))
            .ReturnsAsync(new List<(Guid, string)> { (Guid.NewGuid(), "Mega pack") });

        var act = () => _combos.SaveAsync(Save(combo, (popcorn, 1)));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Mega pack*");
        _uowMock.Verify(u => u.ComboItemStore.ReplaceForComboAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<ComboItem>>()), Times.Never);
    }

    // ── SaveAsync success ───────────────────────────────────────────────────

    [Fact]
    public async Task Save_Valid_ReplacesRecipeAndSetsFlag()
    {
        var combo = Food("Combo");
        var popcorn = Food("Popcorn");
        var cola = Food("Cola");
        Known(combo, popcorn, cola);
        IReadOnlyList<ComboItem>? saved = null;
        _uowMock.Setup(u => u.ComboItemStore.ReplaceForComboAsync(combo.Id, It.IsAny<IReadOnlyList<ComboItem>>()))
            .Callback((Guid _, IReadOnlyList<ComboItem> items) => saved = items)
            .Returns(Task.CompletedTask);

        await _combos.SaveAsync(Save(combo, (popcorn, 2), (cola, 1)));

        combo.IsCombo.Should().BeTrue();
        saved.Should().NotBeNull();
        saved!.Select(s => (s.ComponentId, s.Quantity)).Should().BeEquivalentTo(new[] { (popcorn.Id, 2), (cola.Id, 1) });
        _uowMock.Verify(u => u.CommitTransactionAsync(), Times.Once);
        _uowMock.Verify(u => u.RollbackTransactionAsync(), Times.Never);
    }

    [Fact]
    public async Task Save_NotCombo_ClearsRecipeAndFlag()
    {
        var combo = Food("Combo", isCombo: true);
        Known(combo);

        await _combos.SaveAsync(new SaveComboRequest { ComboId = combo.Id, IsCombo = false });

        combo.IsCombo.Should().BeFalse();
        _uowMock.Verify(u => u.ComboItemStore.ReplaceForComboAsync(combo.Id, It.Is<IReadOnlyList<ComboItem>>(l => l.Count == 0)), Times.Once);
    }

    // ── FoodAndDrinkManager guards ──────────────────────────────────────────

    [Fact]
    public async Task Delete_ComponentUsedByCombo_ThrowsNamingTheCombo()
    {
        var cola = Food("Cola");
        _uowMock.Setup(u => u.ComboItemStore.GetCombosUsingAsync(cola.Id))
            .ReturnsAsync(new List<(Guid, string)> { (Guid.NewGuid(), "Pack A"), (Guid.NewGuid(), "Pack B") });

        var act = () => _foods.DeleteAsync(cola.Id);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Used in combo(s): Pack A, Pack B");
        _uowMock.Verify(u => u.FoodAndDrinkStore.DeleteAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Delete_UnusedItem_Deletes()
    {
        var cola = Food("Cola");
        _uowMock.Setup(u => u.FoodAndDrinkStore.DeleteAsync(cola.Id)).ReturnsAsync(cola);

        await _foods.DeleteAsync(cola.Id);

        _uowMock.Verify(u => u.FoodAndDrinkStore.DeleteAsync(cola.Id), Times.Once);
    }

    private static UpdateFoodAndDrinkRequest MoveTo(FoodAndDrink item, Guid theater)
    {
        return new UpdateFoodAndDrinkRequest { Id = item.Id, TheaterId = theater, Name = item.Name, Price = 1 };
    }

    [Fact]
    public async Task Update_TheaterChangeOnCombo_Throws()
    {
        var combo = Food("Combo", isCombo: true);
        Known(combo);

        var act = () => _foods.UpdateAsync(MoveTo(combo, TheaterB));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Update_TheaterChangeOnComponent_Throws()
    {
        var cola = Food("Cola");
        Known(cola);
        _uowMock.Setup(u => u.ComboItemStore.GetCombosUsingAsync(cola.Id))
            .ReturnsAsync(new List<(Guid, string)> { (Guid.NewGuid(), "Pack A") });

        var act = () => _foods.UpdateAsync(MoveTo(cola, TheaterB));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Pack A*");
    }

    [Fact]
    public async Task Update_TheaterChangeOnTrackedItem_Throws()
    {
        var cola = Food("Cola", tracked: true);
        Known(cola);

        var act = () => _foods.UpdateAsync(MoveTo(cola, TheaterB));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Update_TheaterChangeOnItemWithStockHistory_Throws()
    {
        var cola = Food("Cola");
        Known(cola);
        _uowMock.Setup(u => u.StockMovementStore.ExistsAsync(It.IsAny<Expression<Func<StockMovement, bool>>>()))
            .ReturnsAsync(true);

        var act = () => _foods.UpdateAsync(MoveTo(cola, TheaterB));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Update_TheaterChangeOnPlainItem_Succeeds()
    {
        var cola = Food("Cola");
        Known(cola);

        var dto = await _foods.UpdateAsync(MoveTo(cola, TheaterB));

        dto.TheaterId.Should().Be(TheaterB);
    }

    [Fact]
    public async Task Update_SameTheater_SkipsGuards()
    {
        var combo = Food("Combo", isCombo: true);
        Known(combo);

        var dto = await _foods.UpdateAsync(MoveTo(combo, TheaterA));

        dto.Name.Should().Be("Combo");
        dto.IsCombo.Should().BeTrue();
    }

    // ── Availability ────────────────────────────────────────────────────────

    private static ComboItem Line(FoodAndDrink combo, FoodAndDrink component, int qty)
    {
        return new ComboItem { ComboId = combo.Id, ComponentId = component.Id, Quantity = qty };
    }

    private static (int? Available, bool OutOfStock) Compute(FoodAndDrink item, ComboItem[] recipe, params FoodAndDrink[] components)
    {
        return ComboAvailability.Compute(item, recipe, components.ToDictionary(c => c.Id));
    }

    [Fact]
    public void Availability_UntrackedPlainItem_IsUnlimited()
    {
        Compute(Food("Cola"), Array.Empty<ComboItem>()).Should().Be(((int?)null, false));
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(7, 7, false)]
    [InlineData(250, 10, false)]
    public void Availability_TrackedPlainItem_CapsAtTen(int onHand, int expected, bool outOfStock)
    {
        Compute(Food("Cola", tracked: true, onHand: onHand), Array.Empty<ComboItem>()).Should().Be(((int?)expected, outOfStock));
    }

    [Fact]
    public void Availability_Combo_IsMinOverTrackedComponents()
    {
        var combo = Food("Combo", isCombo: true);
        var popcorn = Food("Popcorn", tracked: true, onHand: 9);   // 9 / 2 = 4
        var cola = Food("Cola", tracked: true, onHand: 30);        // 30 / 1 = 30
        var straw = Food("Straw");                                  // untracked, ignored

        var result = Compute(combo, new[] { Line(combo, popcorn, 2), Line(combo, cola, 1), Line(combo, straw, 1) }, popcorn, cola, straw);

        result.Should().Be(((int?)4, false));
    }

    [Fact]
    public void Availability_Combo_CapsAtTen()
    {
        var combo = Food("Combo", isCombo: true);
        var cola = Food("Cola", tracked: true, onHand: 500);

        Compute(combo, new[] { Line(combo, cola, 1) }, cola).Should().Be(((int?)10, false));
    }

    [Fact]
    public void Availability_Combo_WithoutTrackedComponents_IsUnlimited()
    {
        var combo = Food("Combo", isCombo: true);
        var straw = Food("Straw");

        Compute(combo, new[] { Line(combo, straw, 1) }, straw).Should().Be(((int?)null, false));
    }

    [Fact]
    public void Availability_Combo_ComponentShortOfRecipe_IsOutOfStock()
    {
        var combo = Food("Combo", isCombo: true);
        var popcorn = Food("Popcorn", tracked: true, onHand: 1);

        Compute(combo, new[] { Line(combo, popcorn, 2) }, popcorn).Should().Be(((int?)0, true));
    }

    [Fact]
    public void Availability_Combo_UnavailableComponent_IsOutOfStock()
    {
        var combo = Food("Combo", isCombo: true);
        var straw = Food("Straw", available: false);
        var cola = Food("Cola", tracked: true, onHand: 5);

        Compute(combo, new[] { Line(combo, straw, 1), Line(combo, cola, 1) }, straw, cola).Should().Be(((int?)5, true));
    }

    [Fact]
    public async Task GetById_Combo_UsesConstantQueriesForRecipe()
    {
        var combo = Food("Combo", isCombo: true);
        var cola = Food("Cola", tracked: true, onHand: 3);
        Known(combo, cola);
        _uowMock.Setup(u => u.ComboItemStore.GetByCombosAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new List<ComboItem> { Line(combo, cola, 1) });

        var dto = await _foods.GetByIdAsync(combo.Id);

        dto.AvailableQuantity.Should().Be(3);
        dto.IsOutOfStock.Should().BeFalse();
        _uowMock.Verify(u => u.ComboItemStore.GetByCombosAsync(It.IsAny<IReadOnlyCollection<Guid>>()), Times.Once);
        _uowMock.Verify(u => u.FoodAndDrinkStore.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>()), Times.Once);
    }

    [Fact]
    public async Task GetComponents_ReturnsRecipeWithStockOnlyForTracked()
    {
        var combo = Food("Combo", isCombo: true);
        var cola = Food("Cola", tracked: true, onHand: 12);
        var straw = Food("Straw", onHand: 99);
        Known(combo, cola, straw);
        _uowMock.Setup(u => u.ComboItemStore.GetByCombosAsync(It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync(new List<ComboItem> { Line(combo, cola, 2), Line(combo, straw, 1) });

        var result = await _combos.GetComponentsAsync(combo.Id);

        result.Should().HaveCount(2);
        result.Single(r => r.Name == "Cola").QuantityOnHand.Should().Be(12);
        result.Single(r => r.Name == "Cola").Quantity.Should().Be(2);
        result.Single(r => r.Name == "Straw").QuantityOnHand.Should().BeNull();
    }
}
