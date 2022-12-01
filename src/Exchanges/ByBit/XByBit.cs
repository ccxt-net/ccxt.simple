using CCXT.Simple.Base;
using CCXT.Simple.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CCXT.Simple.Exchanges.Bybit
{
    public class XByBit : IExchange
    {
        /*
		 * ByBit Support Markets: USDT,USD
		 *
		 * Rate Limit
		 *     https://bybit-exchange.github.io/docs/inverse/#t-ratelimits
		 *
		 *     Bybit has different IP frequency limits for GET and POST method:
		 *     GET method:
		 *         50 requests per second continuously for 2 minutes
		 *         70 requests per second continuously for 5 seconds
		 *
		 *     POST method:
		 *         20 requests per second continuously for 2 minutes
		 *         50 requests per second continuously for 5 seconds
		 */

        public XByBit(Exchange mainXchg, string apiKey = "", string secretKey = "", string passPhrase = "")
        {
            this.mainXchg = mainXchg;

            this.ApiKey = apiKey;
            this.SecretKey = secretKey;
            this.PassPhrase = passPhrase;
        }

        public Exchange mainXchg
        {
            get;
            set;
        }

        
        public string ExchangeName { get; set; } = "bybit";

        public string ExchangeUrl { get; set; } = "https://api.bybit.com";

        public bool Alive
        {
            get;
            set;
        }

        public string ApiKey
        {
            get;
            set;
        }

        public string SecretKey
        {
            get;
            set;
        }

        public string PassPhrase
        {
            get;
            set;
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public async ValueTask<bool> VerifyCoinNames()
        {
            var _result = false;

            try
            {
                if (!this.mainXchg.exchangeCs.TryGetValue(ExchangeName, out var _queue_info))
                {
                    _queue_info = new QueueInfo
                    {
                        name = ExchangeName,
                        symbols = new List<QueueSymbol>()
                    };

                    this.mainXchg.exchangeCs.TryAdd(ExchangeName, _queue_info);
                }

                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrl}/v2/public/symbols");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_jstring);
                    var _jarray = _jobject["result"].ToObject<JArray>();

                    _queue_info.symbols.Clear();

                    foreach (var s in _jarray)
                    {
                        var _base = s.Value<string>("base_currency");
                        var _quote = s.Value<string>("quote_currency");

                        if (_quote == "USDT" || _quote == "USD")
                        {
                            _queue_info.symbols.Add(new QueueSymbol
                            {
                                symbol = s.Value<string>("name"),
                                compName = _base,
                                baseName = _base,
                                quoteName = _quote,

                                minPrice = s["price_filter"].Value<decimal>("min_price"),
                                maxPrice = s["price_filter"].Value<decimal>("max_price"),
                                tickSize = s["price_filter"].Value<decimal>("tick_size"),

                                minQty = s["lot_size_filter"].Value<decimal>("min_trading_qty"),
                                maxQty = s["lot_size_filter"].Value<decimal>("max_trading_qty"),
                                qtyStep = s["lot_size_filter"].Value<decimal>("qty_step"),

                                makerFee = s.Value<decimal>("maker_fee"),
                                takerFee = s.Value<decimal>("taker_fee")
                            });
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1411);
            }
            finally
            {
                this.Alive = _result;
            }

            return _result;
        }


        public async ValueTask<bool> GetMarkets(Tickers tickers)
        {
            var _result = false;

            try
            {
                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrl}/v2/public/tickers");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jtickers = JsonConvert.DeserializeObject<BookTickers>(_jstring);

                    for (var i = 0; i < tickers.items.Count; i++)
                    {
                        var _ticker = tickers.items[i];
                        if (_ticker.symbol == "X")
                            continue;

                        var _jobject = _jtickers.result.Find(x => x.symbol == _ticker.symbol);
                        if (_jobject != null)
                        {
                            if (_ticker.quoteName == "USDT" || _ticker.quoteName == "USD")
                            {
                                var _price = _jobject.last_price;
                                {
                                    var _ask_price = _jobject.ask_price;
                                    var _bid_price = _jobject.bid_price;

                                    _ticker.lastPrice = _price * tickers.exchgRate;
                                    _ticker.askPrice = _ask_price * tickers.exchgRate;
                                    _ticker.bidPrice = _bid_price * tickers.exchgRate;
                                }

                                var _volume = _jobject.turnover_24h;
                                {
                                    var _prev_volume24h = _ticker.previous24h;
                                    var _next_timestamp = _ticker.timestamp + 60 * 1000;

                                    _volume *= tickers.exchgRate;
                                    _ticker.volume24h = Math.Floor(_volume / mainXchg.Volume24hBase);

                                    var _curr_timestamp = CUnixTime.NowMilli;
                                    if (_curr_timestamp > _next_timestamp)
                                    {
                                        _ticker.volume1m = Math.Floor((_prev_volume24h > 0 ? _volume - _prev_volume24h : 0) / mainXchg.Volume1mBase);

                                        _ticker.timestamp = _curr_timestamp;
                                        _ticker.previous24h = _volume;
                                    }
                                }
                            }
                        }
                        else
                        {
                            this.mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 1412);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1413);
            }

            return _result;
        }

        ValueTask IExchange.CheckState(Tickers tickers)
        {
            throw new NotImplementedException();
        }

        ValueTask<bool> IExchange.GetBookTickers(Tickers tickers)
        {
            throw new NotImplementedException();
        }

        ValueTask<decimal> IExchange.GetPrice(string symbol)
        {
            throw new NotImplementedException();
        }

        ValueTask<bool> IExchange.GetTickers(Tickers tickers)
        {
            throw new NotImplementedException();
        }

        ValueTask<bool> IExchange.GetVolumes(Tickers tickers)
        {
            throw new NotImplementedException();
        }
    }
}