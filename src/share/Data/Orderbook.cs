﻿namespace CCXT.Simple.Data
{
    public class Orderbook
    {
        public Orderbook()
        {
            this.asks = new List<OrderbookItem>();
            this.bids = new List<OrderbookItem>();
        }

        public List<OrderbookItem> asks
        {
            get;
            set;
        }

        public List<OrderbookItem> bids
        {
            get;
            set;
        }
    }

    public class OrderbookItem
    {
        /// <summary>
        /// 
        /// </summary>
        public decimal price
        {
            get;
            set;
        }

        /// <summary>
        /// 
        /// </summary>
        public decimal size
        {
            get;
            set;
        }

        /// <summary>
        /// 
        /// </summary>
        public int volume
        {
            get;
            set;
        }
    }
}