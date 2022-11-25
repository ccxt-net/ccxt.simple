namespace CCXT.Simple.Exchanges.Korbit
{
    public class Services
    {
        public bool deposit
        {
            get; set;
        }

        public bool exchange
        {
            get; set;
        }

        public bool withdrawal
        {
            get; set;
        }

        public string depositStatus
        {
            get; set;
        }

        public string exchangeStatus
        {
            get; set;
        }

        public string withdrawalStatus
        {
            get; set;
        }

        public string brokerStatus
        {
            get; set;
        }
    }

    public class AddressExtraProps
    {
        public string extraAddressField
        {
            get; set;
        }

        public string regexFormat
        {
            get; set;
        }

        public bool required
        {
            get; set;
        }
    }

    public class Currency
    {
        public string id
        {
            get; set;
        }

        public string acronym
        {
            get; set;
        }

        public string name
        {
            get; set;
        }

        public int @decimal
        {
            get; set;
        }

        public int? confirmationCount
        {
            get; set;
        }

        public object withdrawalMaxOut
        {
            get; set;
        }

        public string withdrawalMaxPerRequest
        {
            get; set;
        }

        public string withdrawalTxFee
        {
            get; set;
        }

        public string withdrawalMinOut
        {
            get; set;
        }

        public Services services
        {
            get; set;
        }

        public AddressExtraProps addressExtraProps
        {
            get; set;
        }

        public string addressRegexFormat
        {
            get; set;
        }

        public string type
        {
            get; set;
        }
    }

    public class State
    {
        public List<Currency> currencies
        {
            get; set;
        }
    }
}