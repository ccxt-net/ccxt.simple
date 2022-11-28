namespace CCXT.Simple.Data
{
    public class Tickers
    {
        public Tickers(string exchange) : this(exchange, 0)
        {
        }

        public Tickers(string exchange, int no_coins)
        {
            this.exchange = exchange;
            this.items = (new Ticker[no_coins]).ToList();
        }

        public Tickers(string exchange, List<QueueSymbol> symbols)
            : this(exchange, symbols.Count)
        {
            for (var i = 0; i < symbols.Count; i++)
            {
                this.items[i] = new Ticker
                {
                    symbol = symbols[i].symbol,
                    compName = symbols[i].compName,
                    dispName = symbols[i].dispName,
                    baseName = symbols[i].baseName,
                    quoteName = symbols[i].quoteName,

                    active = true,
                    deposit = true,
                    withdraw = true,

                    orderbook = new Orderbook()
                };
            }
        }

        public string exchange
        {
            get;
            set;
        }

        public long timestamp
        {
            get;
            set;
        }

        public bool connected
        {
            get;
            set;
        }

        public decimal exchgRate
        {
            get;
            set;
        }

        public bool resetCache
        {
            get;
            set;
        }

        public long nextStateCheck
        {
            get;
            set;
        }

        public List<Ticker> items
        {
            get;
            set;
        }
    }

    public class Ticker
    {
        /// <summary>
        /// coin symbol
        /// </summary>
        public string symbol
        {
            get;
            set;
        }

        public string compName
        {
            get;
            set;
        }

        public string dispName
        {
            get;
            set;
        }

        public string baseName
        {
            get;
            set;
        }

        public string quoteName
        {
            get;
            set;
        }

        /// <summary>
        /// best bid price
        /// </summary>
        public decimal bidPrice
        {
            get;
            set;
        }

        /// <summary>
        /// best bid qty
        /// </summary>
        public decimal bidQty
        {
            get;
            set;
        }

        /// <summary>
        /// best ask price
        /// </summary>
        public decimal askPrice
        {
            get;
            set;
        }

        /// <summary>
        /// best ask qty 
        /// </summary>
        public decimal askQty
        {
            get;
            set;
        }

        /// <summary>
        /// last trade price KRW
        /// </summary>
        public decimal lastPrice
        {
            get;
            set;
        }

        /// <summary>
        /// Volume 24 hours
        /// </summary>
        public decimal previous24h
        {
            get;
            set;
        }

        /// <summary>
        /// Volume 24 hours
        /// </summary>
        public decimal volume24h
        {
            get;
            set;
        }

        /// <summary>
        /// Volume 1 minute
        /// </summary>
        public decimal volume1m
        {
            get;
            set;
        }

        /// <summary>
        /// 
        /// </summary>
        public bool active
        {
            get;
            set;
        }

        /// <summary>
        /// 
        /// </summary>
        public bool deposit
        {
            get;
            set;
        }

        /// <summary>
        /// 
        /// </summary>
        public bool withdraw
        {
            get;
            set;
        }

        /// <summary>
        /// 
        /// </summary>
        public long timestamp
        {
            get;
            set;
        }

        public Orderbook orderbook
        {
            get;
            set;
        }
    }

    public class TickerComparer : IEqualityComparer<Ticker>
    {
        public bool Equals(Ticker x, Ticker y)
        {
            if (String.Equals(x.symbol, y.symbol, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return false;
        }

        public int GetHashCode(Ticker ticker)
        {
            return ticker.symbol.GetHashCode();
        }
    }
}