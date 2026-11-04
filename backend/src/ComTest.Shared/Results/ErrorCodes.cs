namespace ComTest.Shared.Results;

/// <summary>Every code here must have a translation in each language of the API's ErrorMessages resources.</summary>
public static class ErrorCodes
{
    public static class Common
    {
        public const string BadRequest = "common.bad_request";
        public const string Validation = "common.validation";
        public const string InvalidSort = "common.invalid_sort";
        public const string Unauthorized = "common.unauthorized";
        public const string Forbidden = "common.forbidden";
        public const string NotFound = "common.not_found";
        public const string MethodNotAllowed = "common.method_not_allowed";
        public const string Conflict = "common.conflict";
        public const string ConcurrencyConflict = "common.concurrency_conflict";
        public const string PayloadTooLarge = "common.payload_too_large";
        public const string UnsupportedMediaType = "common.unsupported_media_type";
        public const string TooManyRequests = "common.too_many_requests";
        public const string Unexpected = "common.unexpected";
        public const string Timeout = "common.timeout";
    }

    public static class Auth
    {
        public const string InvalidCredentials = "auth.invalid_credentials";
        public const string EmailNotConfirmed = "auth.email_not_confirmed";
        public const string InvalidToken = "auth.invalid_token";
        public const string InvalidTwoFactorCode = "auth.invalid_two_factor_code";
        public const string RefreshInvalid = "auth.refresh_invalid";
        public const string RefreshConflict = "auth.refresh_conflict";
        public const string CsrfRejected = "auth.csrf_rejected";
        public const string MfaRequired = "auth.mfa_required";
        public const string WeakPassword = "auth.weak_password";
        public const string CurrentPasswordInvalid = "auth.current_password_invalid";
        public const string SessionNotFound = "auth.session_not_found";
        public const string TwoFactorAlreadyEnabled = "auth.two_factor_already_enabled";
        public const string TwoFactorNotEnabled = "auth.two_factor_not_enabled";
        public const string TwoFactorRequiredForStaff = "auth.two_factor_required_for_staff";
        public const string PhoneCodeInvalid = "auth.phone_code_invalid";
        public const string PhoneTaken = "auth.phone_taken";
        public const string PhoneRateLimited = "auth.phone_rate_limited";
        public const string SmsUnavailable = "auth.sms_unavailable";
    }

    public static class Tenant
    {
        public const string NotFound = "tenant.not_found";
        public const string Suspended = "tenant.suspended";
        public const string Mismatch = "tenant.mismatch";
        public const string Required = "tenant.required";
        public const string SlugTaken = "tenant.slug_taken";
        public const string SlugReserved = "tenant.slug_reserved";
        public const string StaffLimitReached = "tenant.staff_limit_reached";
        public const string NotAMember = "tenant.not_a_member";
        public const string MemberNotFound = "tenant.member_not_found";
        public const string AlreadyAMember = "tenant.already_a_member";
        public const string OwnerCannotBeChanged = "tenant.owner_cannot_be_changed";
        public const string CannotChangeSelf = "tenant.cannot_change_self";
        public const string InvitationInvalid = "tenant.invitation_invalid";
        public const string InvitationNotFound = "tenant.invitation_not_found";
    }

    public static class Report
    {
        public const string InvalidPeriod = "report.invalid_period";
    }

    public static class Catalog
    {
        public const string CategoryNotFound = "catalog.category_not_found";
        public const string BrandNotFound = "catalog.brand_not_found";
        public const string AttributeNotFound = "catalog.attribute_not_found";
        public const string ProductNotFound = "catalog.product_not_found";
        public const string SlugTaken = "catalog.slug_taken";
        public const string SkuTaken = "catalog.sku_taken";
        public const string CodeTaken = "catalog.code_taken";
        public const string CategoryCycle = "catalog.category_cycle";
        public const string CategoryHasChildren = "catalog.category_has_children";
        public const string ProductLimitReached = "catalog.product_limit_reached";
        public const string NotReadyToPublish = "catalog.not_ready_to_publish";
        public const string InvalidAttributeValue = "catalog.invalid_attribute_value";
        public const string InvalidAttributeFilter = "catalog.invalid_attribute_filter";
        public const string RequiredAttributeMissing = "catalog.required_attribute_missing";
    }

    public static class Inventory
    {
        public const string WarehouseNotFound = "inventory.warehouse_not_found";
        public const string WarehouseCodeTaken = "inventory.warehouse_code_taken";
        public const string WarehouseNotEmpty = "inventory.warehouse_not_empty";
        public const string VariantNotFound = "inventory.variant_not_found";
        public const string StockItemNotFound = "inventory.stock_item_not_found";
        public const string InsufficientStock = "inventory.insufficient_stock";
    }

    public static class Shipping
    {
        public const string ZoneNotFound = "shipping.zone_not_found";
        public const string MethodNotFound = "shipping.method_not_found";
        public const string MethodUnavailable = "shipping.method_unavailable";
        public const string AddressRequired = "shipping.address_required";
    }

    public static class Customer
    {
        public const string AddressNotFound = "customer.address_not_found";
        public const string AddressLimitReached = "customer.address_limit_reached";
    }

    public static class Cart
    {
        public const string Empty = "cart.empty";
        public const string ItemUnavailable = "cart.item_unavailable";
        public const string LineNotFound = "cart.line_not_found";
        public const string CurrencyMismatch = "cart.currency_mismatch";
        public const string TooManyLines = "cart.too_many_lines";
    }

    public static class Checkout
    {
        public const string IdempotencyKeyRequired = "checkout.idempotency_key_required";
        public const string ContactRequired = "checkout.contact_required";
        public const string TotalChanged = "checkout.total_changed";
        public const string TooManyOpenOrders = "checkout.too_many_open_orders";
    }

    public static class Order
    {
        public const string NotFound = "order.not_found";
        public const string InvalidTransition = "order.invalid_transition";
        public const string TrackingTokenInvalid = "order.tracking_token_invalid";
    }

    public static class Payment
    {
        public const string NotFound = "payment.not_found";
        public const string MethodUnavailable = "payment.method_unavailable";
        public const string InvalidState = "payment.invalid_state";
        public const string RefundExceedsPaid = "payment.refund_exceeds_paid";
        public const string RefundNotFound = "payment.refund_not_found";
        public const string SameApprover = "payment.same_approver";
    }

    public static class Media
    {
        public const string NotFound = "media.not_found";
        public const string UnsupportedType = "media.unsupported_type";
        public const string TooLarge = "media.too_large";
        public const string QuotaExceeded = "media.quota_exceeded";
        public const string InvalidImage = "media.invalid_image";
        public const string UploadMissing = "media.upload_missing";
        public const string NotReady = "media.not_ready";
        public const string InUse = "media.in_use";
        public const string InvalidState = "media.invalid_state";
        public const string StorageUnavailable = "media.storage_unavailable";
    }

    public static class Password
    {
        public const string TooShort = "password.too_short";
        public const string TooCommon = "password.too_common";
        public const string Breached = "password.breached";
        public const string ContainsIdentity = "password.contains_identity";
        public const string Rejected = "password.rejected";
    }
}
