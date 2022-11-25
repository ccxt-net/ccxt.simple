namespace CCXT.Simple.Exchanges.Bittrex
{
    public class Market
    {
        public string symbol { get; set; }
        public string baseCurrencySymbol { get; set; }
        public string quoteCurrencySymbol { get; set; }
        public string minTradeSize { get; set; }
        public int precision { get; set; }
        public string status { get; set; }
        public DateTime createdAt { get; set; }
    }

    public class Currency
    {
        public string symbol { get; set; }
        public string name { get; set; }
        public string coinType { get; set; }
        public string status { get; set; }
        public int minConfirmations { get; set; }
        public string notice { get; set; }
        public decimal txFee { get; set; }
        public string logoUrl { get; set; }
        public string baseAddress { get; set; }
    }
}