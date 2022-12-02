namespace CCXT.Simple.Exchanges.Kucoin
{
    public class Symbol
    {
        public string symbol { get; set; }
        public string name { get; set; }
        public string baseCurrency { get; set; }
        public string quoteCurrency { get; set; }
        public string feeCurrency { get; set; }
        public string market { get; set; }
        public decimal baseMinSize { get; set; }
        public decimal quoteMinSize { get; set; }
        public decimal baseMaxSize { get; set; }
        public decimal quoteMaxSize { get; set; }
        public decimal baseIncrement { get; set; }
        public decimal quoteIncrement { get; set; }
        public decimal priceIncrement { get; set; }
        public decimal priceLimitRate { get; set; }
        public decimal? minFunds { get; set; }
        public bool isMarginEnabled { get; set; }
        public bool enableTrading { get; set; }
    }

    public class CoinInfor
    {
        public string code { get; set; }
        public List<Symbol> data { get; set; }
    }
}