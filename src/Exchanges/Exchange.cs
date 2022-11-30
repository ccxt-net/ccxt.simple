using CCXT.Simple.Data;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace CCXT.Simple.Exchanges
{
    public class MessageEventArgs : EventArgs
    {
        public string level;
        public int error_no;
        public string exchange;
        public string message;

        public MessageEventArgs(string exchange, string message, int error_no, string level)
        {
            this.exchange = exchange;
            this.message = message;
            this.error_no = error_no;
            this.level = level;
        }
    }

    public class PriceEventArgs : EventArgs
    {
        public decimal price;

        public PriceEventArgs(decimal price)
        {
            this.price = price;
        }
    }

    public class Exchange
    {
        public Exchange()
        {
            this.UserAgent = "ccxt.simple.sdk";
            this.Volume24hBase = 1000000;
            this.Volume1mBase= 10000;
            this.FiatVSCoinRate = 1;

            this.exchangeCs = new ConcurrentDictionary<string, QueueInfo>();
            this.exchangeQs = new ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentQueue<Tickers>>>();
            this.loggerQs = new ConcurrentDictionary<string, ConcurrentQueue<XLogger>>();
        }

        public event EventHandler<MessageEventArgs> MessageEvent;
        public event EventHandler<PriceEventArgs> UsdPriceEvent;
        public event EventHandler<PriceEventArgs> KrwPriceEvent;

        public ConcurrentDictionary<string, QueueInfo> exchangeCs
        {
            get;
            set;
        }
        public ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentQueue<Tickers>>> exchangeQs
        {
            get;
            set;
        }

        public ConcurrentDictionary<string, ConcurrentQueue<XLogger>> loggerQs
        {
            get;
            set;
        }

        public JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore
        };

        public int ApiCallDelaySeconds
        {
            get; set;
        }

        public decimal Volume24hBase
        {
            get; set;
        }

        public decimal Volume1mBase
        {
            get; set;
        }

        public decimal FiatVSCoinRate
        {
            get; set;
        }

        public string UserAgent
        {
            get; set;
        }

        public decimal usd_btc_price
        {
            get; set;
        }

        public decimal btc_krw_price
        {
            get; set;
        }

        public void OnMessageEvent(string exchange, Exception ex, int error_no)
        {
            MessageEvent?.Invoke(this, new MessageEventArgs(exchange, ex.Message, error_no, "X"));
        }

        public void OnMessageEvent(string exchange, string message, int error_no)
        {
            MessageEvent?.Invoke(this, new MessageEventArgs(exchange, message, error_no, "L"));
        }

        public void OnUsdPriceEvent(decimal price)
        {
            if (price > 0)
            {
                this.usd_btc_price = price;
                UsdPriceEvent?.Invoke(this, new PriceEventArgs(price));
            }
        }

        public void OnKrwPriceEvent(decimal price)
        {
            if (price > 0)
            {
                this.btc_krw_price = price;
                KrwPriceEvent?.Invoke(this, new PriceEventArgs(price));
            }
        }

        public List<QueueSymbol> GetSymbols(string exchange_name)
        {
            var _result = new List<QueueSymbol>();

            var _comparer = new QueueSymbolComparer();

            foreach (var e in this.exchangeCs)
            {
                if (e.Value.name == exchange_name)
                    _result.AddRange(e.Value.symbols.Where(x => x.symbol != "X").Except(_result, _comparer));
            }

            return _result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal ExchangeRate()
        {
            return (usd_btc_price > 0.0m ? btc_krw_price / usd_btc_price : 0.0m) * this.FiatVSCoinRate;
        }

        public string ToQueryString2(Dictionary<string, string> args)
        {
            return String.Join("&", args.Select(a => $"{a.Key}={Uri.EscapeDataString((a.Value ?? "").ToString())}"));
        }

        public string ConvertHexString(byte[] buffer)
        {
            return BitConverter.ToString(buffer).Replace("-", "");
        }
    }
}