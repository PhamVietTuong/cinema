using Cinema.Business.Helpers;
using Cinema.Data.Entities;
using FluentAssertions;

namespace Cinema.Business.Tests;

public class SeatNumberingTests
{
    private static readonly Guid RoomId = Guid.NewGuid();

    private static Seat Seat(string row, int col, Guid? groupId = null, bool isActive = true, SeatKind kind = SeatKind.Standard)
        => new() { Id = Guid.NewGuid(), RoomId = RoomId, RowName = row, ColIndex = col, SeatGroupId = groupId, IsActive = isActive, Kind = kind };

    [Fact]
    public void SingleThenPairThenSingle_GivesOneTwoTwoThree()
    {
        var single1 = Seat("C", 1);
        var groupId = Guid.NewGuid();
        var pairA = Seat("C", 2, groupId);
        var pairB = Seat("C", 3, groupId);
        var single2 = Seat("C", 4);

        var numbers = SeatNumbering.Compute([single1, pairA, pairB, single2]);

        numbers[single1.Id].Should().Be(1);
        numbers[pairA.Id].Should().Be(2);
        numbers[pairB.Id].Should().Be(2);
        numbers[single2.Id].Should().Be(3);
    }

    [Fact]
    public void TwoSeparatePairs_EachCountsAsOneSlot()
    {
        var g1 = Guid.NewGuid();
        var g2 = Guid.NewGuid();
        var pair1A = Seat("C", 1, g1);
        var pair1B = Seat("C", 2, g1);
        var single = Seat("C", 3);
        var pair2A = Seat("C", 4, g2);
        var pair2B = Seat("C", 5, g2);

        var numbers = SeatNumbering.Compute([pair1A, pair1B, single, pair2A, pair2B]);

        numbers[pair1A.Id].Should().Be(1);
        numbers[pair1B.Id].Should().Be(1);
        numbers[single.Id].Should().Be(2);
        numbers[pair2A.Id].Should().Be(3);
        numbers[pair2B.Id].Should().Be(3);
    }

    [Fact]
    public void AnInactiveSeat_StillTakesANumber()
    {
        var blocked = Seat("A", 1, isActive: false);
        var next = Seat("A", 2);

        var numbers = SeatNumbering.Compute([blocked, next]);

        numbers[blocked.Id].Should().Be(1);
        numbers[next.Id].Should().Be(2);
    }

    [Fact]
    public void AGroupOfThree_EachSeatIsStandalone()
    {
        var gid = Guid.NewGuid();
        var a = Seat("A", 1, gid);
        var b = Seat("A", 2, gid);
        var c = Seat("A", 3, gid);

        var numbers = SeatNumbering.Compute([a, b, c]);

        numbers[a.Id].Should().Be(1);
        numbers[b.Id].Should().Be(2);
        numbers[c.Id].Should().Be(3);
    }

    [Fact]
    public void APairSplitByAnotherSeat_EachSeatIsStandalone()
    {
        var gid = Guid.NewGuid();
        var a = Seat("A", 1, gid);
        var middle = Seat("A", 2);
        var b = Seat("A", 3, gid);

        var numbers = SeatNumbering.Compute([a, middle, b]);

        numbers[a.Id].Should().Be(1);
        numbers[middle.Id].Should().Be(2);
        numbers[b.Id].Should().Be(3);
    }

    [Fact]
    public void AGroupWithOnlyOneMemberInTheRow_IsStandalone()
    {
        var gid = Guid.NewGuid();
        var lone = Seat("A", 1, gid);
        var next = Seat("A", 2);

        var numbers = SeatNumbering.Compute([lone, next]);

        numbers[lone.Id].Should().Be(1);
        numbers[next.Id].Should().Be(2);
    }

    [Fact]
    public void TwoRows_AreNumberedIndependently()
    {
        var a1 = Seat("A", 1);
        var a2 = Seat("A", 2);
        var b1 = Seat("B", 1);

        var numbers = SeatNumbering.Compute([a1, a2, b1]);

        numbers[a1.Id].Should().Be(1);
        numbers[a2.Id].Should().Be(2);
        numbers[b1.Id].Should().Be(1);
    }

    [Fact]
    public void InputOrderDoesNotMatter_SeatsAreSortedByColIndex()
    {
        var a1 = Seat("A", 1);
        var a2 = Seat("A", 2);
        var a3 = Seat("A", 3);

        var numbers = SeatNumbering.Compute([a3, a1, a2]);

        numbers[a1.Id].Should().Be(1);
        numbers[a2.Id].Should().Be(2);
        numbers[a3.Id].Should().Be(3);
    }

    [Fact]
    public void AnAisle_TakesNoNumber()
    {
        var a1 = Seat("A", 1);
        var aisle = Seat("A", 2, kind: SeatKind.Aisle);
        var a3 = Seat("A", 3);

        var numbers = SeatNumbering.Compute([a1, aisle, a3]);

        numbers[a1.Id].Should().Be(1);
        numbers[aisle.Id].Should().Be(0);
        numbers[a3.Id].Should().Be(2);
    }

    [Fact]
    public void APairSplitByAnAisle_IsStandalone()
    {
        var gid = Guid.NewGuid();
        var a = Seat("A", 1, gid);
        var aisle = Seat("A", 2, kind: SeatKind.Aisle);
        var b = Seat("A", 3, gid);

        var numbers = SeatNumbering.Compute([a, aisle, b]);

        numbers[a.Id].Should().Be(1);
        numbers[aisle.Id].Should().Be(0);
        numbers[b.Id].Should().Be(2);
    }

    [Fact]
    public void Label_JoinsRowNameAndNumber()
    {
        SeatNumbering.Label("C", 2).Should().Be("C2");
    }

    [Fact]
    public void TicketLabel_UsesTheFrozenValueWhenSet()
    {
        var ticket = new InvoiceTicket { SeatLabel = "C2", Seat = Seat("C", 4) };

        SeatNumbering.TicketLabel(ticket).Should().Be("C2");
    }

    [Fact]
    public void TicketLabel_FallsBackToPhysicalWhenNotSet()
    {
        var ticket = new InvoiceTicket { SeatLabel = null, Seat = Seat("C", 4) };

        SeatNumbering.TicketLabel(ticket).Should().Be("C4");
    }

    [Fact]
    public void TicketLabel_EmptyWhenNeitherIsAvailable()
    {
        var ticket = new InvoiceTicket { SeatLabel = null, Seat = null! };

        SeatNumbering.TicketLabel(ticket).Should().Be(string.Empty);
    }
}
