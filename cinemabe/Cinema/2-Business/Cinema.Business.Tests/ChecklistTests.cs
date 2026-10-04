using Cinema.Business.Contracts.Exceptions;
using Cinema.Business.DTO.Operations;
using Cinema.Business.Managers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;
using FluentAssertions;
using Moq;

namespace Cinema.Business.Tests;

public class ChecklistTests
{
    private readonly Mock<IApplicationUnitOfWork> _uowMock = new();
    private readonly ChecklistManager _sut;
    private readonly Guid _theaterId = Guid.NewGuid();
    private readonly Guid _showTimeId = Guid.NewGuid();
    private readonly Guid _roomId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly List<ChecklistRun> _addedRuns = new();

    public ChecklistTests()
    {
        _sut = new ChecklistManager(_uowMock.Object);
        _uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);
        _uowMock.Setup(u => u.UserStore.GetNamesByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>())).ReturnsAsync(new Dictionary<Guid, string>());
        _uowMock.Setup(u => u.ShowTimeStore.GetShowTimeRoomAsync(_showTimeId, _roomId)).ReturnsAsync(new ShowTimeRoom
        {
            ShowTimeId = _showTimeId,
            RoomId = _roomId,
            Room = new Room { Id = _roomId, TheaterId = _theaterId }
        });
        _uowMock.Setup(u => u.ChecklistStore.AddRunAsync(It.IsAny<ChecklistRun>()))
            .Callback<ChecklistRun>(run => _addedRuns.Add(run))
            .Returns(Task.CompletedTask);
    }

    private ChecklistTemplate GivenTemplate(ChecklistKind kind = ChecklistKind.PreShow)
    {
        var template = new ChecklistTemplate { Id = Guid.NewGuid(), TheaterId = _theaterId, Name = "Pre-show check", Kind = kind };
        template.Items.Add(new ChecklistTemplateItem { Id = Guid.NewGuid(), SortOrder = 1, Text = "Test sound", IsRequired = true });
        template.Items.Add(new ChecklistTemplateItem { Id = Guid.NewGuid(), SortOrder = 0, Text = "Clean floor", IsRequired = true });
        template.Items.Add(new ChecklistTemplateItem { Id = Guid.NewGuid(), SortOrder = 2, Text = "Water the plants", IsRequired = false });
        _uowMock.Setup(u => u.ChecklistStore.GetActiveTemplateAsync(_theaterId, kind)).ReturnsAsync(template);
        return template;
    }

    private OpenChecklistRequest OpenRequest(ChecklistKind kind = ChecklistKind.PreShow)
    {
        return new OpenChecklistRequest { ShowTimeId = _showTimeId, RoomId = _roomId, Kind = kind };
    }

    [Fact]
    public async Task Open_FirstTime_CreatesRunLazilyFromActiveTemplate_InOrder()
    {
        var template = GivenTemplate();

        var run = await _sut.OpenAsync(_theaterId, OpenRequest());

        _addedRuns.Should().ContainSingle();
        run.TemplateName.Should().Be(template.Name);
        run.Items.Select(i => i.Text).Should().ContainInOrder("Clean floor", "Test sound", "Water the plants");
        run.Items.Should().OnlyContain(i => !i.IsDone);
        run.CompletedAt.Should().BeNull();
    }

    [Fact]
    public async Task Open_SecondTime_ReturnsExistingRun_WithoutCreatingAnother()
    {
        GivenTemplate();
        var existing = new ChecklistRun { Id = Guid.NewGuid(), TheaterId = _theaterId, ShowTimeId = _showTimeId, RoomId = _roomId, TemplateName = "x" };
        _uowMock.Setup(u => u.ChecklistStore.GetRunAsync(_showTimeId, _roomId, ChecklistKind.PreShow)).ReturnsAsync(existing);

        var run = await _sut.OpenAsync(_theaterId, OpenRequest());

        run.Id.Should().Be(existing.Id);
        _addedRuns.Should().BeEmpty();
    }

    [Fact]
    public async Task Open_WithoutActiveTemplate_Is404()
    {
        var act = () => _sut.OpenAsync(_theaterId, OpenRequest(ChecklistKind.PostShow));

        await act.Should().ThrowAsync<KeyNotFoundException>();
        _addedRuns.Should().BeEmpty();
    }

    [Fact]
    public async Task Open_ShowTimeOfAnotherTheater_Is403()
    {
        GivenTemplate();

        var act = () => _sut.OpenAsync(Guid.NewGuid(), OpenRequest());

        await act.Should().ThrowAsync<AccessDeniedException>();
    }

    private ChecklistRun GivenOpenRun(bool allDone)
    {
        var run = new ChecklistRun { Id = Guid.NewGuid(), TheaterId = _theaterId, ShowTimeId = _showTimeId, RoomId = _roomId, TemplateName = "x" };
        run.Items.Add(new ChecklistRunItem { Id = Guid.NewGuid(), SortOrder = 0, Text = "a", IsRequired = true, IsDone = allDone });
        run.Items.Add(new ChecklistRunItem { Id = Guid.NewGuid(), SortOrder = 1, Text = "b", IsRequired = false, IsDone = false });
        _uowMock.Setup(u => u.ChecklistStore.GetRunByIdAsync(run.Id)).ReturnsAsync(run);
        foreach (var item in run.Items)
        {
            _uowMock.Setup(u => u.ChecklistStore.GetRunByItemAsync(item.Id)).ReturnsAsync(run);
        }
        return run;
    }

    [Fact]
    public async Task SetItem_TicksItem_RecordsWhoAndWhen_AndUntickClearsThem()
    {
        var run = GivenOpenRun(allDone: false);
        var item = run.Items.First();

        var dto = await _sut.SetItemAsync(new[] { _theaterId }, _userId, new SetChecklistItemRequest { RunItemId = item.Id, IsDone = true, Note = " ok " });

        item.IsDone.Should().BeTrue();
        item.DoneByUserId.Should().Be(_userId);
        item.DoneAt.Should().NotBeNull();
        item.Note.Should().Be("ok");
        dto.Items.First(i => i.Id == item.Id).IsDone.Should().BeTrue();

        await _sut.SetItemAsync(null, _userId, new SetChecklistItemRequest { RunItemId = item.Id, IsDone = false });

        item.IsDone.Should().BeFalse();
        item.DoneByUserId.Should().BeNull();
        item.DoneAt.Should().BeNull();
    }

    [Fact]
    public async Task SetItem_RunOfAnotherTheater_Is403()
    {
        var run = GivenOpenRun(allDone: false);

        var act = () => _sut.SetItemAsync(new[] { Guid.NewGuid() }, _userId, new SetChecklistItemRequest { RunItemId = run.Items.First().Id, IsDone = true });

        await act.Should().ThrowAsync<AccessDeniedException>();
    }

    [Fact]
    public async Task Complete_WithRequiredItemsMissing_Is400()
    {
        var run = GivenOpenRun(allDone: false);

        var act = () => _sut.CompleteAsync(null, _userId, new CompleteChecklistRequest { RunId = run.Id });

        await act.Should().ThrowAsync<InvalidOperationException>();
        run.CompletedAt.Should().BeNull();
    }

    [Fact]
    public async Task Complete_WhenRequiredItemsDone_StampsRun_AndFurtherEditsAre400()
    {
        var run = GivenOpenRun(allDone: true);

        var dto = await _sut.CompleteAsync(null, _userId, new CompleteChecklistRequest { RunId = run.Id });

        dto.CompletedAt.Should().NotBeNull();
        dto.CompletedByUserId.Should().Be(_userId);
        var edit = () => _sut.SetItemAsync(null, _userId, new SetChecklistItemRequest { RunItemId = run.Items.First().Id, IsDone = false });
        await edit.Should().ThrowAsync<InvalidOperationException>();
        var again = () => _sut.CompleteAsync(null, _userId, new CompleteChecklistRequest { RunId = run.Id });
        await again.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SaveTemplate_NewActiveTemplate_StagesItWithOrderedItems()
    {
        ChecklistTemplate? staged = null;
        _uowMock.Setup(u => u.ChecklistStore.StageTemplate(It.IsAny<ChecklistTemplate>())).Callback<ChecklistTemplate>(t => staged = t);

        var dto = await _sut.SaveTemplateAsync(null, _theaterId, new SaveChecklistTemplateRequest
        {
            Name = " Pre ",
            Kind = ChecklistKind.PreShow,
            Items = new List<ChecklistTemplateItemDTO> { new() { Text = "one" }, new() { Text = "two", IsRequired = false } }
        });

        staged.Should().NotBeNull();
        staged!.TheaterId.Should().Be(_theaterId);
        dto.Name.Should().Be("Pre");
        dto.Items.Select(i => i.Text).Should().ContainInOrder("one", "two");
        dto.Items[1].IsRequired.Should().BeFalse();
    }

    [Fact]
    public async Task SaveTemplate_SecondActiveOfSameKind_Is400()
    {
        _uowMock.Setup(u => u.ChecklistStore.HasActiveTemplateAsync(_theaterId, ChecklistKind.PreShow, null)).ReturnsAsync(true);

        var act = () => _sut.SaveTemplateAsync(null, _theaterId, new SaveChecklistTemplateRequest
        {
            Name = "Another",
            Kind = ChecklistKind.PreShow,
            Items = new List<ChecklistTemplateItemDTO> { new() { Text = "one" } }
        });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SaveTemplate_EditOfAnotherTheatersTemplate_Is403()
    {
        var template = new ChecklistTemplate { Id = Guid.NewGuid(), TheaterId = Guid.NewGuid(), Name = "x" };
        _uowMock.Setup(u => u.ChecklistStore.GetTemplateAsync(template.Id)).ReturnsAsync(template);

        var act = () => _sut.SaveTemplateAsync(new[] { _theaterId }, _theaterId, new SaveChecklistTemplateRequest
        {
            Id = template.Id,
            Name = "Renamed",
            Items = new List<ChecklistTemplateItemDTO> { new() { Text = "one" } }
        });

        await act.Should().ThrowAsync<AccessDeniedException>();
    }
}
