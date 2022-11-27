namespace CCXT.Simple.Exchanges.Coinone
{
    public class COOrderbookItem
    {
        public decimal price { get; set; }
        public decimal qty { get; set; }
    }

    public class COTicker
    {
        public string quote_currency { get; set; }
        public string target_currency { get; set; }
        public long timestamp { get; set; }
        public decimal high { get; set; }
        public decimal low { get; set; }
        public decimal first { get; set; }
        public decimal last { get; set; }
        public decimal quote_volume { get; set; }
        public decimal target_volume { get; set; }
        public List<COOrderbookItem> best_asks { get; set; }
        public List<COOrderbookItem> best_bids { get; set; }
        public string id { get; set; }
    }

    public class COTickers
    {
        public string result { get; set; }
        public int error_code { get; set; }
        public long server_time { get; set; }
        public List<COTicker> tickers { get; set; }
    }
}