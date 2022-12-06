using CCXT.Simple.Base;
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
            this.exchangeTs = new ConcurrentDictionary<string, Tickers>();
            this.exchangeQs = new ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentQueue<Tickers>>>();
            this.loggerQs = new ConcurrentDictionary<string, ConcurrentQueue<XLogger>>();

            this.exchangeBs = new ConcurrentDictionary<string, CompData>();
            this.exchangesNs = new ConcurrentDictionary<string, ChainData>();
        }

        public event EventHandler<MessageEventArgs> MessageEvent;
        public event EventHandler<PriceEventArgs> UsdPriceEvent;
        public event EventHandler<PriceEventArgs> KrwPriceEvent;

        public ConcurrentDictionary<string, QueueInfo> exchangeCs
        {
            get;
            set;
        }

        public ConcurrentDictionary<string, Tickers> exchangeTs
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

        public ConcurrentDictionary<string, CompData> exchangeBs
        {
            get;
            set;
        }

        public ConcurrentDictionary<string, ChainData> exchangesNs
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

        public decimal krw_btc_price
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
                this.krw_btc_price = price;
                KrwPriceEvent?.Invoke(this, new PriceEventArgs(price));
            }
        }

        public ConcurrentDictionary<string, ConcurrentQueue<Tickers>> GetXQueues(string exchange_name)
        {
            var _result = (ConcurrentDictionary<string, ConcurrentQueue<Tickers>>)null;

            if (!this.exchangeQs.ContainsKey(exchange_name))
            {
                _result = new ConcurrentDictionary<string, ConcurrentQueue<Tickers>>();
                this.exchangeQs.TryAdd(exchange_name, _result);
            }
            else
            {
                _result = this.exchangeQs[exchange_name];
            }

            return _result;
        }

        public QueueInfo GetXInfors(string exchange_name)
        {
            var _result = (QueueInfo)null;

            if (!this.exchangeCs.ContainsKey(exchange_name))
            {
                _result = new QueueInfo
                {
                    exchange = exchange_name,
                    symbols = new List<QueueSymbol>()
                };

                this.exchangeCs.TryAdd(exchange_name, _result);
            }
            else
            {
                _result = this.exchangeCs[exchange_name];
            }

            return _result;
        }

        public Tickers GetTickers(string exchange_name)
        {
            var _result = (Tickers)null;

            if (!this.exchangeTs.ContainsKey(exchange_name))
            {
                var _infor = this.GetXInfors(exchange_name);
                _result = new Tickers(exchange_name, _infor.symbols);

                this.exchangeTs.TryAdd(exchange_name, _result);
            }
            else
            {
                _result = this.exchangeTs[exchange_name];
            }

            return _result;
        }

        public async ValueTask<bool> UpdateProtocols(Tickers tickers)
        {
            var _result = false;

            if (this.exchangesNs.ContainsKey(tickers.exchange))
            {
                var _xls_data = this.exchangesNs[tickers.exchange];
                if (_xls_data.items.Count > 0)
                {
                    foreach (var c in _xls_data.items)
                    {
                        var _states = tickers.states.Where(x => x.currency == c.baseName);
                        if (_states.Count() > 0)
                        {
                            foreach (var s in _states)
                            {
                                foreach (var n in c.networks)
                                {
                                    var _b = s.networks.FirstOrDefault(x => x.network == n.network && x.protocol == n.protocol);
                                    if (_b == null)
                                    {
                                        s.networks.Add(new WNetwork
                                        {
                                            name = $"{s.currency}-{n.network}",
                                            network = n.network,
                                            protocol = n.protocol,

                                            deposit = s.deposit,
                                            withdraw = s.withdraw
                                        });
                                    }
                                    else
                                    {
                                        _b.deposit = s.deposit;
                                        _b.withdraw = s.withdraw;
                                    }
                                }
                            }
                        }
                    }

                    _result = true;
                }
            }

            await Task.Delay(0);

            return _result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal ExchangeRate()
        {
            return (usd_btc_price > 0.0m ? krw_btc_price / usd_btc_price : 0.0m) * this.FiatVSCoinRate;
        }

        public string ToQueryString2(Dictionary<string, string> args)
        {
            return args != null ? String.Join("&", args.Select(a => $"{a.Key}={Uri.EscapeDataString((a.Value ?? "").ToString())}"))
                                : "";
        }

        public string ConvertHexString(byte[] buffer)
        {
            return BitConverter.ToString(buffer).Replace("-", "");
        }
    }
}