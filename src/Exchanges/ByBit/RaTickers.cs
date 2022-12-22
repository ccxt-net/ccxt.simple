namespace CCXT.Simple.Exchanges.Bybit
{
    /// <summary>
    /// https://api.bybit.com/spot/v3/public/quote/ticker/24hr
    /// </summary>
    public class RaTickers
    {
        public int retCode { get; set; }
        public string retMsg { get; set; }
        public RTResult result { get; set; }
        public long time { get; set; }
    }

    public class RTResult
    {
        public List<RaTicker> list { get; set; }
    }

    public class RaTicker
    {
        /// <summary>
        /// Current timestamp, unit in millisecond
        /// </summary>
        public long t { get; set; }

        /// <summary>
        /// Name of the trading pair
        /// </summary>
        public string s { get; set; }

        /// <summary>
        /// Last traded price
        /// </summary>
        public decimal lp { get; set; }

        /// <summary>
        /// Best bid price
        /// </summary>
        public decimal bp { get; set; }

        /// <summary>
        /// Best ask price
        /// </summary>
        public decimal ap { get; set; }

        /// <summary>
        /// Open price
        /// </summary>
        public decimal o { get; set; }

        /// <summary>
        /// High price
        /// </summary>
        public decimal h { get; set; }

        /// <summary>
        /// Low price
        /// </summary>
        public decimal l { get; set; }

        /// <summary>
        /// Trading volume
        /// </summary>
        public decimal v { get; set; }

        /// <summary>
        /// Trading quote volume
        /// </summary>
        public decimal qv { get; set; }
    }
}