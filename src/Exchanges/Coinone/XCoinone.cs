using CCXT.Simple.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace CCXT.Simple.Exchanges.Coinone
{
    public class XCoinone : IExchange
    {
        /*
		 * Coinone Support Markets: KRW
		 *
		 * API https://doc.coinone.co.kr/
		 *
		 * Public API
		 *     Rate Limit: 300 requests per minute
		 *
		 * Private API
		 *     Rate Limit: 10 requests per second
		 *
		 */

        public XCoinone(Exchange mainXchg, string apiKey = "", string secretKey = "", string passPhrase = "")
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
        
        public string ExchangeName { get; set; } = "coinone";

        public string ExchangeUrl { get; set; } = "https://api.coinone.co.kr";
        public string ExchangeUrlTb { get; set; } = "https://tb.coinone.co.kr";

        public bool Alive { get; set; }
        public string ApiKey { get; set; }
        public string SecretKey { get; set; }
        public string PassPhrase { get; set; }
        

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public async ValueTask<bool> VerifySymbols()
        {
            var _result = false;

            try
            {
                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrlTb}/api/v1/tradepair/");
                    var _jstring = await _response.Content.ReadAsStringAsync();

                    var _jobject = JObject.Parse(_jstring);
                    var _jarray = _jobject["tradepairs"].ToObject<JArray>();

                    var _queue_info = this.mainXchg.GetXInfors(ExchangeName);

                    foreach (JToken s in _jarray)
                    {
                        var _base_name = s.Value<string>("target_coin_symbol");
                        var _quote_name = s.Value<string>("base_coin_symbol");

                        _queue_info.symbols.Add(new QueueSymbol
                        {
                            symbol = $"{_base_name}-{_quote_name}",
                            compName = _base_name,
                            baseName = _base_name,
                            quoteName = _quote_name,
                            tickSize = s.Value<decimal>("price_unit")
                        });
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 3501);
            }
            finally
            {
                this.Alive = _result;
            }

            return _result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="states"></param>
        /// <returns></returns>
        public async ValueTask<bool> VerifyStates(Tickers tickers)
        {
            var _result = false;

            try
            {
                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrlTb}/api/v1/coin/");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_jstring);
                    var _jarray = _jobject["coins"].ToObject<JArray>();

                    foreach (var s in _jarray)
                    {
                        var _currency = s.Value<string>("symbol");

                        var _state = tickers.states.SingleOrDefault(x => x.currency == _currency);
                        if (_state == null)
                        {
                            _state = new WState
                            {
                                currency = _currency,
                                active = s.Value<bool>("is_activate"),
                                deposit = s.Value<bool>("is_deposit"),
                                withdraw = s.Value<bool>("is_withdraw"),
                                networks = new List<WNetwork>()
                            };

                            tickers.states.Add(_state);
                        }
                        else
                        {
                            _state.active = s.Value<bool>("is_activate");
                            _state.deposit = s.Value<bool>("is_deposit");
                            _state.withdraw = s.Value<bool>("is_withdraw");
                        }

                        var _t_items = tickers.items.Where(x => x.compName == _state.currency);
                        if (_t_items != null)
                        {
                            foreach (var t in _t_items)
                            {
                                t.active = _state.active;
                                t.deposit = _state.deposit;
                                t.withdraw = _state.withdraw;
                            }
                        }
                    }

                    _result = true;
                }

                this.mainXchg.OnMessageEvent(ExchangeName, $"checking deposit & withdraw status...", 3502);
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 3503);
            }

            return _result;
        }

        /// <summary>
        /// Get Last Price
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public async ValueTask<decimal> GetPrice(string symbol)
        {
            var _result = 0.0m;

            try
            {
                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrl}/ticker?currency=" + symbol);
                    var _tstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_tstring);

                    _result = _jobject.Value<decimal>("last");

                    Debug.Assert(_result != 0.0m);
                }
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 3504);
            }

            return _result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public async ValueTask<(OrderbookItem best_ask, OrderbookItem best_bid)> GetOrderbook(string symbol)
        {
            var _result = (best_ask: new OrderbookItem(), best_bid: new OrderbookItem());

            try
            {
                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrl}/orderbook?currency=" + symbol);
                    var _tstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_tstring);

                    if (_jobject.Value<string>("result") == "success")
                    {
                        var _asks = _jobject["ask"].ToObject<List<OrderbookItem>>();
                        {
                            _result.best_ask = _asks.OrderBy(x => x.price).First();
                        }

                        var _bids = _jobject["bid"].ToObject<List<OrderbookItem>>();
                        {
                            _result.best_bid = _bids.OrderBy(x => x.price).Last();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 3505);
            }

            return _result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="tickers"></param>
        /// <returns></returns>
        public async ValueTask<bool> GetMarkets(Tickers tickers)
        {
            var _result = false;

            try
            {
                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrl}/public/v2/ticker_new/KRW");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jdata = JsonConvert.DeserializeObject<Markets>(_jstring);

                    for (var i = 0; i < tickers.items.Count; i++)
                    {
                        var _ticker = tickers.items[i];
                        if (_ticker.symbol == "X")
                            continue;

                        var _jitem = _jdata.tickers.FirstOrDefault(x => x.target_currency.ToUpper() == _ticker.baseName);
                        if (_jitem != null)
                        {
                            var _price = _jitem.last;
                            {
                                _ticker.lastPrice = _price;
                                _ticker.askPrice = _jitem.best_asks.Count > 0 ? _jitem.best_asks[0].price : 0;
                                _ticker.bidPrice = _jitem.best_bids.Count > 0 ? _jitem.best_bids[0].price : 0;
                            }

                            var _volume = _jitem.quote_volume;
                            {
                                var _prev_volume24h = _ticker.previous24h;
                                var _next_timestamp = _ticker.timestamp + 60 * 1000;

                                _ticker.volume24h = Math.Floor(_volume / mainXchg.Volume24hBase);

                                var _curr_timestamp = _jitem.timestamp;
                                if (_curr_timestamp > _next_timestamp)
                                {
                                    _ticker.volume1m = Math.Floor((_prev_volume24h > 0 ? _volume - _prev_volume24h : 0) / mainXchg.Volume1mBase);

                                    _ticker.timestamp = _curr_timestamp;
                                    _ticker.previous24h = _volume;
                                }
                            }
                        }
                        else
                        {
                            this.mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 3506);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 3507);
            }

            return _result;
        }

        ValueTask<bool> IExchange.GetBookTickers(Tickers tickers)
        {
            throw new NotImplementedException();
        }

        ValueTask<bool> IExchange.GetMarkets(Tickers tickers)
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