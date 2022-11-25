using CCXT.Simple.Base;
using CCXT.Simple.Data;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace CCXT.Simple.Exchanges.Bithumb
{
    public class XBithumb : IExchange
    {
        /*
		 * Bithumb Support Markets: KRW, BTC
		 *
		 * Rate Limit
		 *     https://apidocs.bithumb.com/docs/rate_limits
		 *
		 * Public API
		 *     1초당 최대 135회 요청 가능합니다.
		 *     초과 요청을 보내면 API 사용이 제한됩니다.
		 *
		 * Private API
		 *     1초당 최대 15회 요청 가능합니다.
		 *     초과 요청을 보내면 API 사용이 일시적으로 제한됩니다. (Public - 1분 / Private info - 5분 / Private trade - 10분)
		 */

        public XBithumb(Exchange mainXchg, string apiKey, string secretKey)
        {
            this.mainXchg = mainXchg;

            this.ApiKey = apiKey;
            this.SecretKey = secretKey;
        }

        private Exchange mainXchg
        {
            get;
            set;
        }
        
        public string ExchangeName { get; set; } = "bithumb";

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
                    _queue_info.symbols.Clear();

                    using HttpResponseMessage _k_response = await _wc.GetAsync("https://api.bithumb.com/public/ticker/ALL_KRW");
                    var _k_jstring = await _k_response.Content.ReadAsStringAsync();
                    var _k_jobject = JObject.Parse(_k_jstring);

                    foreach (JProperty s in _k_jobject["data"].Children())
                    {
                        if (!((JObject)s.Value).ContainsKey("opening_price"))
                            continue;

                        var _symbol = s.Name;

                        _queue_info.symbols.Add(new QueueSymbol
                        {
                            symbol = $"{_symbol}_KRW",
                            name = $"{_symbol}/KRW",
                            baseName = _symbol,
                            quoteName = "KRW"
                        });
                    }

                    using HttpResponseMessage _b_response = await _wc.GetAsync("https://api.bithumb.com/public/ticker/ALL_BTC");
                    var _b_jstring = await _b_response.Content.ReadAsStringAsync();
                    var _b_jobject = JObject.Parse(_b_jstring);

                    foreach (JProperty s in _b_jobject["data"].Children())
                    {
                        if (!((JObject)s.Value).ContainsKey("opening_price"))
                            continue;

                        var _symbol = s.Name;

                        _queue_info.symbols.Add(new QueueSymbol
                        {
                            symbol = $"{_symbol}_BTC",
                            name = $"{_symbol}/BTC",
                            baseName = _symbol,
                            quoteName = "BTC"
                        });
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1211);
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
                    using HttpResponseMessage _response = await _wc.GetAsync("https://api.bithumb.com/public/assetsstatus/ALL");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_jstring);

                    var _jdata = _jobject["data"];

                    foreach (JProperty s in _jdata.Children())
                    {
                        var _currency = s.Name;

                        var _state = states.states.SingleOrDefault(x => x.currency == _currency);
                        if (_state == null)
                        {
                            states.states.Add(new WState
                            {
                                currency = _currency,
                                active = true,
                                deposit = s.Value.Value<int>("deposit_status") > 0,
                                withdraw = s.Value.Value<int>("withdrawal_status") > 0
                            });
                        }
                        else
                        {
                            _state.deposit = s.Value.Value<int>("deposit_status") > 0;
                            _state.withdraw = s.Value.Value<int>("withdrawal_status") > 0;
                        }
                    }
                }

                this.mainXchg.OnMessageEvent(ExchangeName, $"checking deposit & withdraw status...", 1212);
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1213);
            }
        }

        /// <summary>
        /// Get Last Price
        /// </summary>
        /// <returns></returns>
        public async ValueTask<decimal> GetPrice(string symbol)
        {
            var _result = 0.0m;

            try
            {
                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _response = await _wc.GetAsync("https://api.bithumb.com/public/ticker/" + symbol);
                    var _tstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_tstring);

                    _result = _jobject["data"].Value<decimal>("closing_price");

                    Debug.Assert(_result != 0.0m);
                }
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1214);
            }

            return _result;
        }

        public async ValueTask<Bithumb.Orderbook> GetOrderbook(string symbol)
        {
            var _result = new Bithumb.Orderbook();

            try
            {
                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _response = await _wc.GetAsync("https://api.bithumb.com/public/orderbook/" + symbol + "?count=30");
                    var _tstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_tstring);

                    var _asks = _jobject["data"].Value<JArray>("asks");
                    _result.asks.AddRange(
                        _asks.Select(x => new Bithumb.OrderbookItem
                        {
                            price = x.Value<decimal>("price"),
                            quantity = x.Value<decimal>("quantity")
                        })
                    );

                    var _bids = _jobject["data"].Value<JArray>("bids");
                    _result.bids.AddRange(
                        _bids.Select(x => new Bithumb.OrderbookItem
                        {
                            price = x.Value<decimal>("price"),
                            quantity = x.Value<decimal>("quantity")
                        })
                    );
                }
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1215);
            }

            return _result;
        }

        /// <summary>
        /// Get Bithumb Best Book Tickers
        /// </summary>
        /// <returns></returns>
        public async ValueTask<bool> GetBookTickers(Tickers tickers)
        {
            var _result = false;

            try
            {
                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _k_response = await _wc.GetAsync("https://api.bithumb.com/public/orderbook/ALL_KRW?count=1");
                    var _k_jstring = await _k_response.Content.ReadAsStringAsync();
                    var _k_jobject = JObject.Parse(_k_jstring);
                    var _k_data = _k_jobject["data"].ToObject<JObject>();

                    await Task.Delay(100);

                    using HttpResponseMessage _b_response = await _wc.GetAsync("https://api.bithumb.com/public/orderbook/ALL_BTC?count=1");
                    var _b_jstring = await _b_response.Content.ReadAsStringAsync();
                    var _b_jobject = JObject.Parse(_b_jstring);
                    var _b_data = _b_jobject["data"].ToObject<JObject>();

                    for (var i = 0; i < tickers.items.Count; i++)
                    {
                        var _ticker = tickers.items[i];
                        if (_ticker.symbol == "X")
                            continue;

                        var _pairs = _ticker.symbol.Split('_');

                        if (_pairs[1] == "KRW" && _k_data.ContainsKey(_pairs[0]))
                        {
                            var _bid = _k_data[_pairs[0]]["bids"][0];
                            var _ask = _k_data[_pairs[0]]["asks"][0];

                            _ticker.askPrice = _ask.Value<decimal>("price");
                            _ticker.askQty = _ask.Value<decimal>("quantity");
                            _ticker.bidPrice = _bid.Value<decimal>("price");
                            _ticker.bidQty = _bid.Value<decimal>("quantity");
                        }
                        else if (_pairs[1] == "BTC" && _b_data.ContainsKey(_pairs[0]))
                        {
                            var _bid = _b_data[_pairs[0]]["bids"][0];
                            var _ask = _b_data[_pairs[0]]["asks"][0];

                            Debug.Assert(this.mainXchg.btc_krw_price != 0.0m);

                            _ticker.askPrice = _ask.Value<decimal>("price") * mainXchg.btc_krw_price;
                            _ticker.askQty = _ask.Value<decimal>("quantity");
                            _ticker.bidPrice = _bid.Value<decimal>("price") * mainXchg.btc_krw_price;
                            _ticker.bidQty = _bid.Value<decimal>("quantity");
                        }
                        else
                        {
                            this.mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 1216);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1217);
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
                    using HttpResponseMessage _k_response = await _wc.GetAsync("https://api.bithumb.com/public/ticker/ALL_KRW");
                    var _k_tstring = await _k_response.Content.ReadAsStringAsync();
                    var _k_jstring = _k_tstring.Substring(24, _k_tstring.Length - 25);
                    var _k_jobject = JObject.Parse(_k_jstring);

                    await Task.Delay(100);

                    using HttpResponseMessage _b_response = await _wc.GetAsync("https://api.bithumb.com/public/ticker/ALL_BTC");
                    var _b_tstring = await _b_response.Content.ReadAsStringAsync();
                    var _b_jstring = _b_tstring.Substring(24, _b_tstring.Length - 25);
                    var _b_jobject = JObject.Parse(_b_jstring);

                    for (var i = 0; i < tickers.items.Count; i++)
                    {
                        var _ticker = tickers.items[i];
                        if (_ticker.symbol == "X")
                            continue;

                        var _pairs = _ticker.symbol.Split('_');

                        if (_pairs[1] == "KRW" && _k_jobject.ContainsKey(_pairs[0]))
                        {
                            var _price = _k_jobject[_pairs[0]].Value<decimal>("closing_price");
                            _ticker.lastPrice = _price;

                            var _volume = _k_jobject[_pairs[0]].Value<decimal>("acc_trade_value");
                            {
                                var _prev_volume24h = _ticker.previous24h;
                                var _next_timestamp = _ticker.timestamp + 60 * 1000;

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
                        else if (_pairs[1] == "BTC" && _b_jobject.ContainsKey(_pairs[0]))
                        {
                            var _price = _b_jobject[_pairs[0]].Value<decimal>("closing_price");
                            _ticker.lastPrice = _price * mainXchg.btc_krw_price;

                            var _volume = _b_jobject[_pairs[0]].Value<decimal>("acc_trade_value");
                            {
                                var _prev_volume24h = _ticker.previous24h;
                                var _next_timestamp = _ticker.timestamp + 60 * 1000;

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
                            this.mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 1218);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1219);
            }

            return _result;
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