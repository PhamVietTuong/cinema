using Cinema.Business.Helpers;
using Cinema.Data.Contracts;
using Cinema.Data.Entities;
using Cinema.Data.Enums;

namespace Cinema.Business.Managers;

/// <summary>
/// Side effects of an invoice leaving Pending, shared by the booking flow and the admin status override so the
/// two cannot drift apart. The caller owns the surrounding transaction and the atomic status claim.
/// </summary>
internal static class InvoiceLifecycle
{
    // Returns the loyalty points reserved for a booking that did not complete (cancelled/expired/deleted).
    public static async Task RestoreRedeemedPointsAsync(IApplicationUnitOfWork uow, Invoice invoice)
    {
        if (invoice.PointsRedeemed <= 0)
        {
            return;
        }
        if (invoice.UserId is not Guid ownerId)
        {
            return;
        }
        var user = invoice.User ?? await uow.UserStore.GetByIdAsync(ownerId);
        if (user is not null)
        {
            user.Points += invoice.PointsRedeemed;
            await uow.UserStore.UpdateAsync(user);
        }
    }

    // Returns the gift-card balance drawn for a booking that did not complete (cancelled/expired/deleted).
    public static async Task RestoreGiftCardAsync(IApplicationUnitOfWork uow, Invoice invoice)
    {
        if (invoice.GiftCardId is not Guid giftCardId || invoice.GiftCardAmount <= 0)
        {
            return;
        }
        var card = await uow.GiftCardStore.GetByIdAsync(giftCardId);
        if (card is not null)
        {
            card.Balance += invoice.GiftCardAmount;
            await uow.GiftCardStore.UpdateAsync(card);
        }
    }

    // A cancelled or expired invoice no longer has food to hand over.
    public static void CancelFoodOrder(Invoice invoice)
    {
        if (invoice.FoodStatus != FoodOrderStatus.None)
        {
            invoice.FoodStatus = FoodOrderStatus.Cancelled;
        }
    }

    // Loyalty: accrue points on the paid amount and re-evaluate the membership tier (only when the invoice has a
    // customer), then mark the promo code (if any) as consumed. Does not save; returns the customer, if any.
    public static async Task<User?> ApplyPaidSideEffectsAsync(IApplicationUnitOfWork uow, Invoice invoice)
    {
        User? user = null;
        if (invoice.UserId is Guid customerId)
        {
            user = await uow.UserStore.GetByIdAsync(customerId);
            if (user is not null)
            {
                var (pointsPerUnit, _) = await LoyaltyRates.LoadAsync(uow, invoice.TheaterId);
                user.Points += (int)(invoice.FinalAmount / pointsPerUnit);
                var tiers = await uow.MemberShipStore.FindAsync(m => m.MinPoints <= user.Points);
                var tier  = tiers.OrderByDescending(m => m.MinPoints).FirstOrDefault();
                if (tier is not null)
                {
                    user.MemberShipId = tier.Id;
                }
                await uow.UserStore.UpdateAsync(user);
            }
        }

        // Mark the promotion program (if any) as consumed — an atomic increment, not a read-modify-write.
        if (invoice.PromotionProgramId is Guid usedProgramId)
        {
            await uow.PromotionProgramStore.IncrementUsedCountAsync(usedProgramId);
        }
        return user;
    }
}
