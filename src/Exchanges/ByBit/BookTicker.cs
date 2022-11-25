namespace CCXT.Simple.Exchanges.Bybit
{
    public class BookTicker
    {
        public string symbol
        {
            get; set;
        }

        public decimal bid_price
        {
            get; set;
        }

        public decimal ask_price
        {
            get; set;
        }

        public decimal last_price
        {
            get; set;
        }

        public decimal turnover_24h
        {
            get; set;
        }
    }

    public class BookTickers
    {
        public int ret_code
        {
            get; set;
        }

        public string ret_msg
        {
            get; set;
        }

        public List<BookTicker> result
        {
            get; set;
        }
    }
}