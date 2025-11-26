namespace LMS.DTOs
{
    // Request body sent to PhonePe (before Base64)
    public class PhonePePayBody
    {
        public string merchantId { get; set; }
        public string merchantTransactionId { get; set; }
        public string merchantUserId { get; set; }
        public long amount { get; set; } // paise
        public string redirectUrl { get; set; }
        public string callbackUrl { get; set; }
        public string redirectMode { get; set; } = "REDIRECT";
        public string mobileNumber { get; set; }
        public PaymentInstrument paymentInstrument { get; set; }
    }

    public class PaymentInstrument
    {
        public string type { get; set; } = "PAY_PAGE";
    }

    // -------------------------------
    // RESPONSE DTOs
    // -------------------------------
    public class PhonePeInitResponse
    {
        public bool success { get; set; }
        public string code { get; set; }
        public string message { get; set; }
        public PhonePeInitData data { get; set; }
    }

    public class PhonePeInitData
    {
        public string merchantId { get; set; }
        public string merchantTransactionId { get; set; }
        public InstrumentResponse instrumentResponse { get; set; }
    }

    public class InstrumentResponse
    {
        public string type { get; set; }
        public RedirectInfo redirectInfo { get; set; }
    }

    public class RedirectInfo
    {
        public string url { get; set; }
        public string method { get; set; }
    }

    // Frontend → backend
    public class PhonePeInitiateDto
    {
        public string Username { get; set; }      // from LandingRegister response
        public string MobileNumber { get; set; }  // from formData.phoneNumber
        public decimal Amount { get; set; }       // in rupees
    }

    public class PhonePeInitiateResult
    {
        public string RedirectUrl { get; set; }
        public string MerchantOrderId { get; set; }
    }

    public class PaymentStatusDto
    {
        public string MerchantOrderId { get; set; }
        public string Status { get; set; }
        public decimal Amount { get; set; }
        public string Username { get; set; }
        public string PhonePeOrderId { get; set; }
        public string PhonePeTransactionId { get; set; }
        public string StateRaw { get; set; }
        public string Message { get; set; }
        public DateTime? PaymentTimeUtc { get; set; }

        public string InstrumentType { get; set; }  // NET_BANKING
        public string BankId { get; set; }          // ICIC / SBIN
        public string Arn { get; set; }
        public string Brn { get; set; }
    }


}
