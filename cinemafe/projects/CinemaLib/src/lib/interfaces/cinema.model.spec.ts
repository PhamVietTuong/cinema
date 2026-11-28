import { CinemaServiceAgent } from '../services/cinema-http.service';
import { promotionStubCaptionKey } from './cinema.model';

describe('promotionStubCaptionKey', () => {
  it.each([
    [CinemaServiceAgent.PromotionEligibility.Everyone, 'promotions.stubCaption.everyone'],
    [CinemaServiceAgent.PromotionEligibility.Members, 'promotions.stubCaption.members'],
    [CinemaServiceAgent.PromotionEligibility.StudentOrTeacher, 'promotions.stubCaption.studentOrTeacher'],
    [CinemaServiceAgent.PromotionEligibility.AgeLimit, 'promotions.stubCaption.ageLimit'],
  ])('maps %s', (eligibility, key) => {
    expect(promotionStubCaptionKey(eligibility)).toBe(key);
  });

  it('treats a missing or unknown value as everyone', () => {
    expect(promotionStubCaptionKey(undefined)).toBe('promotions.stubCaption.everyone');
    expect(promotionStubCaptionKey(99 as CinemaServiceAgent.PromotionEligibility)).toBe('promotions.stubCaption.everyone');
  });
});
