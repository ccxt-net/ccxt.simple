namespace CCXT.Simple.Exchanges.Kucoin
{
    public class Currency
    {
        public string currency { get; set; }
        public string name { get; set; }
        public string fullName { get; set; }
        public int precision { get; set; }
        public int confirms { get; set; }
        public string contractAddress { get; set; }
        public decimal withdrawalMinSize { get; set; }
        public decimal withdrawalMinFee { get; set; }
        public bool isWithdrawEnabled { get; set; }
        public bool isDepositEnabled { get; set; }
        public bool isMarginEnabled { get; set; }
        public bool isDebitEnabled { get; set; }
    }

    public class CoinState
    {
        public string code { get; set; }
        public List<Currency> data { get; set; }
    }
 }