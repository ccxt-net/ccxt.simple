using CCXT.Simple.Base;
using CCXT.Simple.Data;
using Newtonsoft.Json.Linq;

namespace CCXT.Simple.Exchanges.Kucoin
{
    public class XKucoin : IExchange
    {
        /*
		 * Kucoin Support Markets: USDT, BTC
		 *
		 * REST API
		 *     https://docs.kucoin.com/#general
		 *     https://docs.kucoin.com/#symbol-snapshot
		 *
		 */

        public XKucoin(Exchange mainXchg)
        {
            this.mainXchg = mainXchg;
        }

        private Exchange mainXchg
        {
            get;
            set;
        }

        
        public string ExchangeName { get; set; } = "kucoin";

        public bool Alive
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
                    using HttpResponseMessage _response = await _wc.GetAsync("https://api.kucoin.com/api/v1/symbols");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_jstring);
                    var _jarray = _jobject["data"].ToObject<JArray>();

                    _queue_info.symbols.Clear();

                    foreach (JToken s in _jarray)
                    {
                        var _quote_name = s.Value<string>("quoteCurrency");

                        if (_quote_name == "USDT" || _quote_name == "BTC")
                        {
                            var _symbol = s.Value<string>("symbol");
                            var _base_name = s.Value<string>("baseCurrency");

                            if (_base_name != _symbol.Split('-')[0])
                                _base_name = _symbol.Split('-')[0];

                            _queue_info.symbols.Add(new QueueSymbol
                            {
                                symbol = _symbol,
                                name = s.Value<string>("name"),
                                tickSize = s.Value<decimal>("priceIncrement"),
                                baseName = _base_name,
                                quoteName = _quote_name
                            });
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 2111);
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
        /// <returns></returns>
        public async ValueTask CheckState(WStates states)
        {
            try
            {
                states.exchange = ExchangeName;

                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _response = await _wc.GetAsync("https://api.kucoin.com/api/v1/currencies");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_jstring);

                    var _jarray = _jobject.Value<JArray>("data");
                    foreach (var s in _jarray)
                    {
                        var _currency = s.Value<string>("currency");

                        var _state = states.states.SingleOrDefault(x => x.currency == _currency);
                        if (_state == null)
                        {
                            _state = new WState
                            {
                                currency = _currency,
                                active = true,

                                deposit = s.Value<bool>("isDepositEnabled"),
                                withdraw = s.Value<bool>("isWithdrawEnabled"),

                                networks = new List<WNetwork>()
                            };

                            states.states.Add(_state);
                        }

                        _state.networks.Add(new WNetwork
                        {
                            name = _currency,
                            //network = s.Value<string>("currency"),
                            //protocol = s.Value<string>("currency"),

                            deposit = s.Value<bool>("isDepositEnabled"),
                            withdraw = s.Value<bool>("isWithdrawEnabled"),

                            minWithdrawal = s.Value<decimal>("withdrawalMinSize"),
                            withdrawFee = s.Value<decimal>("withdrawalMinFee"),

                            minConfirm = s.Value<int>("confirms")
                        });
                    }
                }

                this.mainXchg.OnMessageEvent(ExchangeName, $"checking deposit & withdraw status...", 2112);
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 2113);
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public async ValueTask<decimal> GetPrice(string symbol)
        {
            var _result = 0.0m;

            try
            {
                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _response = await _wc.GetAsync("https://api.kucoin.com/api/v1/market/orderbook/level1?symbol=" + symbol);
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_jstring);

                    var _jdata = _jobject["data"];
                    _result = _jobject.Value<decimal>("price");
                }
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 2114);
            }

            return _result;
        }

        /// <summary>
        /// Get Upbit Tickers
        /// </summary>
        /// <param name="tickers"></param>
        /// <returns></returns>
        public async ValueTask<bool> GetTickers(Tickers tickers)
        {
            var _result = false;

            try
            {
                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _response = await _wc.GetAsync("https://api.kucoin.com/api/v1/market/allTickers");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_jstring);

                    var _jdata = _jobject["data"]["ticker"].ToObject<JArray>();

                    for (var i = 0; i < tickers.items.Count; i++)
                    {
                        var _ticker = tickers.items[i];
                        if (_ticker.symbol == "X")
                            continue;

                        var _jitem = _jdata.SingleOrDefault(x => x["symbol"].ToString() == _ticker.symbol);
                        if (_jitem != null)
                        {
                            var _last_price = _jitem.Value<decimal>("last");
                            {
                                if (_ticker.quoteName == "USDT")
                                {
                                    _ticker.lastPrice = _last_price * tickers.exchgRate;
                                }
                                else if (_ticker.quoteName == "BTC")
                                {
                                    _ticker.lastPrice = _last_price * mainXchg.btc_krw_price;
                                }
                            }
                        }
                        else
                        {
                            this.mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 2115);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 2116);
            }

            return _result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="tickers"></param>
        /// <returns></returns>
        public async ValueTask<bool> GetBookTickers(Tickers tickers)
        {
            var _result = false;

            try
            {
                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _response = await _wc.GetAsync("https://api.kucoin.com/api/v1/market/allTickers");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_jstring);

                    var _jdata = _jobject["data"]["ticker"].ToObject<JArray>();

                    for (var i = 0; i < tickers.items.Count; i++)
                    {
                        var _ticker = tickers.items[i];
                        if (_ticker.symbol == "X")
                            continue;

                        var _jitem = _jdata.SingleOrDefault(x => x["symbol"].ToString() == _ticker.symbol);
                        if (_jitem != null)
                        {
                            var _last_price = _jitem.Value<decimal>("last");
                            {
                                var _ask_price = _jitem.Value<decimal>("sell");
                                var _bid_price = _jitem.Value<decimal>("buy");

                                if (_ticker.quoteName == "USDT")
                                {
                                    _ticker.lastPrice = _last_price * tickers.exchgRate;

                                    _ticker.askPrice = _ask_price * tickers.exchgRate;
                                    _ticker.bidPrice = _bid_price * tickers.exchgRate;
                                }
                                else if (_ticker.quoteName == "BTC")
                                {
                                    _ticker.lastPrice = _last_price * mainXchg.btc_krw_price;

                                    _ticker.askPrice = _ask_price * mainXchg.btc_krw_price;
                                    _ticker.bidPrice = _bid_price * mainXchg.btc_krw_price;
                                }
                            }
                        }
                        else
                        {
                            this.mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 2117);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 2118);
            }

            return _result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="tickers"></param>
        /// <returns></returns>
        public async ValueTask<bool> GetVolumes(Tickers tickers)
        {
            var _result = false;

            try
            {
                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _response = await _wc.GetAsync("https://api.kucoin.com/api/v1/market/allTickers");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_jstring);

                    var _jdata = _jobject["data"]["ticker"].ToObject<JArray>();

                    for (var i = 0; i < tickers.items.Count; i++)
                    {
                        var _ticker = tickers.items[i];
                        if (_ticker.symbol == "X")
                            continue;

                        var _jitem = _jdata.SingleOrDefault(x => x["symbol"].ToString() == _ticker.symbol);
                        if (_jitem != null)
                        {
                            var _volume = _jitem.Value<decimal>("volValue");
                            {
                                var _prev_volume24h = _ticker.previous24h;
                                var _next_timestamp = _ticker.timestamp + 60 * 1000;

                                if (_ticker.quoteName == "USDT")
                                    _volume *= tickers.exchgRate;
                                else if (_ticker.quoteName == "BTC")
                                    _volume *= mainXchg.btc_krw_price;

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
                        else
                        {
                            this.mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 2119);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 2120);
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
                    using HttpResponseMessage _response = await _wc.GetAsync("https://api.kucoin.com/api/v1/market/allTickers");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_jstring);

                    var _jdata = _jobject["data"]["ticker"].ToObject<JArray>();

                    for (var i = 0; i < tickers.items.Count; i++)
                    {
                        var _ticker = tickers.items[i];
                        if (_ticker.symbol == "X")
                            continue;

                        var _jitem = _jdata.SingleOrDefault(x => x["symbol"].ToString() == _ticker.symbol);
                        if (_jitem != null)
                        {
                            var _last_price = _jitem.Value<decimal>("last");
                            {
                                var _ask_price = _jitem.Value<decimal>("sell");
                                var _bid_price = _jitem.Value<decimal>("buy");

                                if (_ticker.quoteName == "USDT")
                                {
                                    _ticker.lastPrice = _last_price * tickers.exchgRate;

                                    _ticker.askPrice = _ask_price * tickers.exchgRate;
                                    _ticker.bidPrice = _bid_price * tickers.exchgRate;
                                }
                                else if (_ticker.quoteName == "BTC")
                                {
                                    _ticker.lastPrice = _last_price * mainXchg.btc_krw_price;

                                    _ticker.askPrice = _ask_price * mainXchg.btc_krw_price;
                                    _ticker.bidPrice = _bid_price * mainXchg.btc_krw_price;
                                }
                            }

                            var _volume = _jitem.Value<decimal>("volValue");
                            {
                                var _prev_volume24h = _ticker.previous24h;
                                var _next_timestamp = _ticker.timestamp + 60 * 1000;

                                if (_ticker.quoteName == "USDT")
                                    _volume *= tickers.exchgRate;
                                else if (_ticker.quoteName == "BTC")
                                    _volume *= mainXchg.btc_krw_price;

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
                        else
                        {
                            this.mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 2121);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 2122);
            }

            return _result;
        }
    }
}