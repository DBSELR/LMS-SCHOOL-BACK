using System;

namespace LMS.Models
{
    public class PaymentTransaction
    {
        public int Id { get; set; }

        public string MerchantOrderId { get; set; }   // our generated ORD_...
        public string Username { get; set; }          // from LandingRegister
        public decimal Amount { get; set; }           // rupees

        public string Status { get; set; }            // PENDING / COMPLETED / FAILED
        public string PhonePeOrderId { get; set; }    // orderId from status API
        public string PhonePeTransactionId { get; set; } // first paymentDetails.transactionId
        public string StateRaw { get; set; }          // e.g. COMPLETED, FAILED, CREATED

        public string RawStatusJson { get; set; }     // full JSON from PhonePe (for debugging)

        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
    }
}
