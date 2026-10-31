namespace SwiftCheckoutPOS.BLL.Services;

// Only deliberately safe business messages may be shown by the checkout UI.
public sealed class CheckoutValidationException(string message) : Exception(message);
