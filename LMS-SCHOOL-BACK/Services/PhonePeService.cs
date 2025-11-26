using System.Data;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LMS.DTOs;
using LMS.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using pg_sdk_dotnet;
using pg_sdk_dotnet.Payments.v2;
using pg_sdk_dotnet.Payments.v2.Models.Request;
using pg_sdk_dotnet.Payments.v2.Models.Response;

namespace LMS.Services
{
    public class PhonePeService
    {
        private readonly PhonePeSdkOptions _options;
        private readonly StandardCheckoutClient _checkoutClient;
        private readonly ILogger<PhonePeService> _logger;
        private readonly string _connectionString;

        public PhonePeService(
            IOptions<PhonePeSdkOptions> options,
            ILoggerFactory loggerFactory,
            ILogger<PhonePeService> logger,
            IConfiguration configuration)
        {
            _options = options.Value;
            _logger = logger;
            _connectionString = configuration.GetConnectionString("DefaultConnection");

            var env = string.Equals(_options.Environment, "PRODUCTION",
                    StringComparison.OrdinalIgnoreCase)
                ? Env.PRODUCTION
                : Env.SANDBOX;

            _checkoutClient = StandardCheckoutClient.GetInstance(
                _options.ClientId,
                _options.ClientSecret,
                _options.ClientVersion,
                env,
                loggerFactory
            );
        }

        private string BuildRedirectUrlWithOrder(string merchantOrderId)
        {
            if (string.IsNullOrWhiteSpace(_options.RedirectUrl))
                throw new InvalidOperationException("PhonePe RedirectUrl not configured.");

            var separator = _options.RedirectUrl.Contains("?") ? "&" : "?";
            return $"{_options.RedirectUrl}{separator}merchantOrderId={Uri.EscapeDataString(merchantOrderId)}";
        }

        // Simple POCO for internal use (no EF)
        private class PaymentTxRow
        {
            public string MerchantOrderId { get; set; }
            public string Username { get; set; }
            public decimal Amount { get; set; }
            public string Status { get; set; }
        }

        private async Task InsertPendingTransactionAsync(
            string merchantOrderId, string username, decimal amount)
        {
            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("SP_PhonePe_InsertTransaction", con);
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue("@MerchantOrderId", merchantOrderId);
            cmd.Parameters.AddWithValue("@Username", username);
            cmd.Parameters.AddWithValue("@Amount", amount);
            cmd.Parameters.AddWithValue("@Status", "PENDING");

            await con.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task<PaymentTxRow?> GetTransactionAsync(string merchantOrderId)
        {
            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("SP_PhonePe_GetTransaction", con);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@MerchantOrderId", merchantOrderId);

            await con.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;

            return new PaymentTxRow
            {
                MerchantOrderId = reader["MerchantOrderId"].ToString(),
                Username = reader["Username"].ToString(),
                Amount = reader["Amount"] != DBNull.Value ? (decimal)reader["Amount"] : 0m,
                Status = reader["Status"].ToString()
            };
        }

        private async Task UpdateTransactionStatusAsync(
     string merchantOrderId,
     string mappedStatus,
     decimal amountRupees,
     string state,
     string phonePeOrderId,
     string phonePeTxnId,
     string rawJson,
     DateTime? paymentTimeUtc,
     long? paymentTimestampMs,
     string instrumentType,
     string bankId,
     string arn,
     string brn)
        {
            using var con = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("SP_PhonePe_UpdateTransactionStatus", con);
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue("@MerchantOrderId", merchantOrderId);
            cmd.Parameters.AddWithValue("@Status", (object?)mappedStatus ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Amount", amountRupees);
            cmd.Parameters.AddWithValue("@StateRaw", (object?)state ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PhonePeOrderId", (object?)phonePeOrderId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PhonePeTransactionId", (object?)phonePeTxnId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@RawStatusJson", (object?)rawJson ?? DBNull.Value);

            cmd.Parameters.AddWithValue(
                "@PhonePePaymentTimeUtc",
                (object?)paymentTimeUtc ?? DBNull.Value
            );

            cmd.Parameters.AddWithValue(
                "@PhonePePaymentTimestampMs",
                (object?)paymentTimestampMs ?? DBNull.Value
            );

            // 🆕 new instrument fields
            cmd.Parameters.AddWithValue("@InstrumentType", (object?)instrumentType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@BankId", (object?)bankId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Arn", (object?)arn ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Brn", (object?)brn ?? DBNull.Value);

            await con.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }


        /// <summary>
        /// 1) Insert PENDING row via SP
        /// 2) Call PhonePe SDK to generate payment URL
        /// 3) Return redirectUrl + MerchantOrderId
        /// </summary>
        public async Task<PhonePeInitiateResult> InitiatePaymentAsync(
            string username,
            decimal amountRupees)
        {
            if (amountRupees <= 0)
                throw new ArgumentException("Amount must be > 0", nameof(amountRupees));

            var amountPaise = (long)(amountRupees * 100);
            var merchantOrderId =
                   //$"DBS_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{username}";
                   $"DBS_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

            // 1️⃣ Save in DB as PENDING via SP
            await InsertPendingTransactionAsync(merchantOrderId, username, amountRupees);

            // 2️⃣ Redirect URL for after PhonePe payment
            var redirectWithOrder = BuildRedirectUrlWithOrder(merchantOrderId);

            var payRequest = StandardCheckoutPayRequest
                .Builder()
                .SetMerchantOrderId(merchantOrderId)
                .SetAmount(amountPaise)
                .SetRedirectUrl(redirectWithOrder)
                .SetExpireAfter(300)
                .SetMessage("5Mantra LMS Admission Fee")
                .Build();

            _logger.LogInformation("PhonePe PayRequest: {PayRequest}",
                JsonSerializer.Serialize(payRequest));

            StandardCheckoutPayResponse response =
                await _checkoutClient.Pay(payRequest);

            _logger.LogInformation("PhonePe PayResponse: {Response}",
                JsonSerializer.Serialize(response));

            if (response == null || string.IsNullOrWhiteSpace(response.RedirectUrl))
                throw new Exception("PhonePe: redirectUrl missing in response.");

            return new PhonePeInitiateResult
            {
                RedirectUrl = response.RedirectUrl,
                MerchantOrderId = merchantOrderId
            };
        }

        /// <summary>
        /// Uses PhonePe Order Status API, updates DB via SP, returns DTO to frontend.
        /// </summary>
        public async Task<PaymentStatusDto> GetAndUpdatePaymentStatusAsync(string merchantOrderId)
        {
            var txn = await GetTransactionAsync(merchantOrderId);
            if (txn == null)
                throw new Exception("Payment transaction not found for this order id.");

            // 1️⃣ Get OAuth token
            var accessToken = await FetchAccessTokenAsync();

            // 2️⃣ Call Order Status API
            var baseStatusUrl = string.Equals(_options.Environment, "PRODUCTION",
                    StringComparison.OrdinalIgnoreCase)
                ? "https://api.phonepe.com/apis/pg"
                : "https://api-preprod.phonepe.com/apis/pg-sandbox";

            var url = $"{baseStatusUrl}/checkout/v2/order/{merchantOrderId}/status";

            using var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Authorization = new AuthenticationHeaderValue("O-Bearer", accessToken);

            if (!string.IsNullOrWhiteSpace(_options.MerchantId))
            {
                request.Headers.Add("X-MERCHANT-ID", _options.MerchantId);
            }

            var response = await client.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("PhonePe OrderStatus for {OrderId}: {Json}",
                merchantOrderId, json);

            if (!response.IsSuccessStatusCode)
            {
                // keep old status, just log raw json
                await UpdateTransactionStatusAsync(
                    merchantOrderId,
                    txn.Status,
                    txn.Amount,
                    $"HTTP {(int)response.StatusCode}",
                    null,
                    null,
                    json,
                    null,    // paymentTimeUtc
                    null,    // paymentTimestampMs
                    null,    // instrumentType
                    null,    // bankId
                    null,    // arn
                    null     // brn
                );

                throw new Exception("Failed to fetch order status from PhonePe.");
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var state = root.GetProperty("state").GetString(); // COMPLETED / FAILED / PENDING
            var amountPaise = root.GetProperty("amount").GetInt64();
            var amountRupees = amountPaise / 100m;

            string phonePeOrderId = root.TryGetProperty("orderId", out var orderIdEl)
                ? orderIdEl.GetString()
                : null;

            string phonePeTxnId = null;
            DateTime? paymentTimeUtc = null;
            long? paymentTimestampMs = null;

            string instrumentType = null;
            string bankId = null;
            string arn = null;
            string brn = null;

            if (root.TryGetProperty("paymentDetails", out var details) &&
                details.ValueKind == JsonValueKind.Array &&
                details.GetArrayLength() > 0)
            {
                var first = details[0];

                if (first.TryGetProperty("transactionId", out var txEl))
                {
                    phonePeTxnId = txEl.GetString();
                }

                // timestamp → date
                if (first.TryGetProperty("timestamp", out var tsEl) &&
                    tsEl.ValueKind == JsonValueKind.Number)
                {
                    var ts = tsEl.GetInt64();
                    paymentTimestampMs = ts;
                    var dto = DateTimeOffset.FromUnixTimeMilliseconds(ts);
                    paymentTimeUtc = dto.UtcDateTime;
                }

                // 🆕 splitInstruments[0].instrument.{type,bankId,arn,brn}
                if (first.TryGetProperty("splitInstruments", out var splitArr) &&
                    splitArr.ValueKind == JsonValueKind.Array &&
                    splitArr.GetArrayLength() > 0)
                {
                    var firstSplit = splitArr[0];

                    if (firstSplit.TryGetProperty("instrument", out var instr) &&
                        instr.ValueKind == JsonValueKind.Object)
                    {
                        if (instr.TryGetProperty("type", out var typeEl))
                            instrumentType = typeEl.GetString();

                        if (instr.TryGetProperty("bankId", out var bankIdEl))
                            bankId = bankIdEl.GetString();

                        if (instr.TryGetProperty("arn", out var arnEl))
                            arn = arnEl.GetString();

                        if (instr.TryGetProperty("brn", out var brnEl))
                            brn = brnEl.GetString();
                    }
                }
            }

            var mappedStatus = state switch
            {
                "COMPLETED" => "COMPLETED",
                "FAILED" => "FAILED",
                _ => "PENDING"
            };

            // 3️⃣ Update DB with everything
            await UpdateTransactionStatusAsync(
                merchantOrderId,
                mappedStatus,
                amountRupees,
                state,
                phonePeOrderId,
                phonePeTxnId,
                json,
                paymentTimeUtc,
                paymentTimestampMs,
                instrumentType,
                bankId,
                arn,
                brn
            );

            var msg = state switch
            {
                "COMPLETED" => "Payment successful",
                "FAILED" => "Payment failed",
                _ => "Payment is in progress"
            };

            return new PaymentStatusDto
            {
                MerchantOrderId = merchantOrderId,
                Status = mappedStatus,
                Amount = amountRupees,
                Username = txn.Username,
                PhonePeOrderId = phonePeOrderId,
                PhonePeTransactionId = phonePeTxnId,
                StateRaw = state,
                Message = msg,
                PaymentTimeUtc = paymentTimeUtc,
                InstrumentType = instrumentType, // 🆕 if you add to DTO
                BankId = bankId,
                Arn = arn,
                Brn = brn
            };
        }


        private async Task<string> FetchAccessTokenAsync()
        {
            var baseAuthUrl = string.Equals(_options.Environment, "PRODUCTION",
                    StringComparison.OrdinalIgnoreCase)
                ? "https://api.phonepe.com/apis/pg"
                : "https://api-preprod.phonepe.com/apis/pg-sandbox";

            var url = $"{baseAuthUrl}/v1/oauth/token";  // OAuth for Standard Checkout :contentReference[oaicite:1]{index=1}

            using var client = new HttpClient();
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", _options.ClientId),
                new KeyValuePair<string, string>("client_version", _options.ClientVersion.ToString()),
                new KeyValuePair<string, string>("client_secret", _options.ClientSecret),
                new KeyValuePair<string, string>("grant_type", "client_credentials")
            });

            var resp = await client.PostAsync(url, content);
            var json = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError("PhonePe auth token failed: {Status} {Body}",
                    resp.StatusCode, json);
                throw new Exception("Unable to get PhonePe access token.");
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("access_token", out var tokenEl))
                return tokenEl.GetString();

            throw new Exception("PhonePe access token missing in response.");
        }
    }
}
