namespace CCXT.Simple.Exchanges.Coinone
{
    public class OrderbookItem
    {
        public decimal price { get; set; }
        public decimal qty { get; set; }
    }

    public class Market
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
        public List<OrderbookItem> best_asks { get; set; }
        public List<OrderbookItem> best_bids { get; set; }
        public string id { get; set; }
    }

    public class Markets
    {
        public string result { get; set; }
        public int error_code { get; set; }
        public long server_time { get; set; }
        public List<Market> tickers { get; set; }
    }
}