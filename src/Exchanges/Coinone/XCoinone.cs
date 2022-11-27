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
                var _queue_info = (QueueInfo)null;
                if (!this.mainXchg.exchangeCs.TryGetValue(ExchangeName, out _queue_info))
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
                    using HttpResponseMessage _response = await _wc.GetAsync("https://tb.coinone.co.kr/api/v1/tradepair/");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_jstring);
                    var _jarray = _jobject["tradepairs"].ToObject<JArray>();

                    _queue_info.symbols.Clear();

                    foreach (JToken s in _jarray)
                    {
                        var _base_name = s.Value<string>("target_coin_symbol");
                        var _quote_name = s.Value<string>("base_coin_symbol");

                        _queue_info.symbols.Add(new QueueSymbol
                        {
                            symbol = $"{_base_name}-{_quote_name}",
                            name = $"{_base_name}/{_quote_name}",
                            tickSize = s.Value<decimal>("price_unit"),
                            baseName = _base_name,
                            quoteName = _quote_name
                        });
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1611);
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
        public async ValueTask CheckState(WStates states)
        {
            try
            {
                states.exchange = ExchangeName;

                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _response = await _wc.GetAsync("https://tb.coinone.co.kr/api/v1/coin/");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_jstring);
                    var _jarray = _jobject["coins"].ToObject<JArray>();

                    foreach (var s in _jarray)
                    {
                        var _currency = s.Value<string>("symbol");

                        var _state = states.states.SingleOrDefault(x => x.currency == _currency);
                        if (_state == null)
                        {
                            states.states.Add(new WState
                            {
                                currency = _currency,
                                active = s.Value<bool>("is_activate"),
                                deposit = s.Value<bool>("is_deposit"),
                                withdraw = s.Value<bool>("is_withdraw")
                            });
                        }
                        else
                        {
                            _state.active = s.Value<bool>("is_activate");
                            _state.deposit = s.Value<bool>("is_deposit");
                            _state.withdraw = s.Value<bool>("is_withdraw");
                        }
                    }
                }

                this.mainXchg.OnMessageEvent(ExchangeName, $"checking deposit & withdraw status...", 1612);
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1613);
            }
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
                    using HttpResponseMessage _response = await _wc.GetAsync("https://api.coinone.co.kr/ticker?currency=" + symbol);
                    var _tstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_tstring);

                    _result = _jobject.Value<decimal>("last");

                    Debug.Assert(_result != 0.0m);
                }
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1614);
            }

            return _result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public async ValueTask<(Market best_ask, Market best_bid)> GetOrderbook(string symbol)
        {
            var _result = (best_ask: new Market(), best_bid: new Market());

            try
            {
                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _response = await _wc.GetAsync("https://api.coinone.co.kr/orderbook?currency=" + symbol);
                    var _tstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_tstring);

                    if (_jobject.Value<string>("result") == "success")
                    {
                        var _asks = _jobject["ask"].ToObject<List<Market>>();
                        {
                            _result.best_ask = _asks.OrderBy(x => x.price).First();
                        }

                        var _bids = _jobject["bid"].ToObject<List<Market>>();
                        {
                            _result.best_bid = _bids.OrderBy(x => x.price).Last();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1615);
            }

            return _result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="tickers"></param>
        /// <param name="wstates"></param>
        /// <returns></returns>
        public async ValueTask<bool> GetMarkets(Data.Tickers tickers, WStates wstates)
        {
            var _result = false;

            try
            {
                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _response = await _wc.GetAsync("https://api.coinone.co.kr/public/v2/ticker_new/KRW");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jdata = JsonConvert.DeserializeObject<COTickers>(_jstring);

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
                                _ticker.askPrice = _jitem.best_asks[0].price;
                                _ticker.bidPrice = _jitem.best_bids[0].price;
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
                            this.mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 1616);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1617);
            }

            return _result;
        }

        ValueTask<bool> IExchange.GetBookTickers(Data.Tickers tickers)
        {
            throw new NotImplementedException();
        }

        ValueTask<bool> IExchange.GetMarkets(Data.Tickers tickers)
        {
            throw new NotImplementedException();
        }

        ValueTask<bool> IExchange.GetTickers(Data.Tickers tickers)
        {
            throw new NotImplementedException();
        }

        ValueTask<bool> IExchange.GetVolumes(Data.Tickers tickers)
        {
            throw new NotImplementedException();
        }
    }
}