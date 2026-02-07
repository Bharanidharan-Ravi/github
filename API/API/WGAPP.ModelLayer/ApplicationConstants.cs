using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WGAPP.ModelLayer
{
    public class ApplicationConstants
    {
        public const string missing_token_error = "User token is either missing or invalid";

        public const string success_message = "OK";

        public const string prod_env = "Production";
        public const string stage_env = "Staging";

        public const string create_customer_sequence_name = "OCRD";

        public const string payment_mode_cash = "Cash";
        public const string indian_currency_format = "INR";

        public const string SQ_Post = "SQ Posting";
        public const string SO_Post = "S0 Posting";

        public static string[] outGoingPaymentApprovalTypes = new string[] { string.Empty, "Approved", "Declined" };
        public const string Add = "Add";
        public const string Update = "Update";
        public const string Delete = "Delete";
    }
}
