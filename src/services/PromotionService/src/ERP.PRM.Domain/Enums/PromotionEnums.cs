namespace ERP.PRM.Domain.Enums;

public enum DiscountType { Percentage = 0, FixedAmount = 1, BuyXGetY = 2, FreeShipping = 3 }
public enum PromotionStatus { Draft = 0, Active = 1, Paused = 2, Expired = 3, Cancelled = 4 }
public enum LoyaltyTier { Bronze = 0, Silver = 1, Gold = 2, Platinum = 3 }
public enum PromotionScope { Order = 0, Product = 1, Category = 2 }
public enum FlashSaleStatus { Scheduled = 0, Active = 1, Completed = 2, Cancelled = 3 }
