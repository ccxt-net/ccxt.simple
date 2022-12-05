namespace CCXT.Simple.Exchanges.Bybit
{
    /// <summary>
    /// https://api.bybit.com/v2/public/symbols
    /// </summary>
    public class CoinInfor
    {
        public int ret_code { get; set; }
        public string ret_msg { get; set; }
        public List<CIResult> result { get; set; }
        public string ext_code { get; set; }
        public string ext_info { get; set; }
        public string time_now { get; set; }
    }

    public class LeverageFilter
    {
        public decimal min_leverage { get; set; }
        public decimal max_leverage { get; set; }
        public decimal leverage_step { get; set; }
    }

    public class LotSizeFilter
    {
        public decimal max_trading_qty { get; set; }
        public decimal min_trading_qty { get; set; }
        public decimal qty_step { get; set; }
        public decimal post_only_max_trading_qty { get; set; }
    }

    public class PriceFilter
    {
        public decimal min_price { get; set; }
        public decimal max_price { get; set; }
        public decimal tick_size { get; set; }
    }

    public class CIResult
    {
        public string name { get; set; }
        public string alias { get; set; }
        public string status { get; set; }
        public string base_currency { get; set; }
        public string quote_currency { get; set; }
        public int price_scale { get; set; }
        public decimal taker_fee { get; set; }
        public decimal maker_fee { get; set; }
        public int funding_interval { get; set; }
        public LeverageFilter leverage_filter { get; set; }
        public PriceFilter price_filter { get; set; }
        public LotSizeFilter lot_size_filter { get; set; }
    }
}