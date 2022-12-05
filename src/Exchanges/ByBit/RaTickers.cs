namespace CCXT.Simple.Exchanges.Bybit
{
    /// <summary>
    /// https://api.bybit.com/v2/public/tickers
    /// </summary>
    public class RaTickers
    {
        public int ret_code { get; set; }
        public string ret_msg { get; set; }
        public List<RaResult> result { get; set; }
        public string ext_code { get; set; }
        public string ext_info { get; set; }
        public string time_now { get; set; }
    }

    public class RaResult
    {
        public string symbol { get; set; }
        public decimal bid_price { get; set; }
        public decimal ask_price { get; set; }
        public decimal last_price { get; set; }
        public string last_tick_direction { get; set; }
        public decimal prev_price_24h { get; set; }
        public decimal price_24h_pcnt { get; set; }
        public decimal high_price_24h { get; set; }
        public decimal low_price_24h { get; set; }
        public decimal prev_price_1h { get; set; }
        public decimal mark_price { get; set; }
        public decimal index_price { get; set; }
        public decimal open_interest { get; set; }
        public int countdown_hour { get; set; }
        public decimal turnover_24h { get; set; }
        public decimal volume_24h { get; set; }
        public decimal funding_rate { get; set; }
        public decimal predicted_funding_rate { get; set; }
        public DateTime next_funding_time { get; set; }
        public decimal predicted_delivery_price { get; set; }
        public decimal total_turnover { get; set; }
        public decimal total_volume { get; set; }
        public decimal delivery_fee_rate { get; set; }
        public string delivery_time { get; set; }
        public string price_1h_pcnt { get; set; }
        public string open_value { get; set; }
    }
}