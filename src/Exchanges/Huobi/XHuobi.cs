using CCXT.Simple.Base;
using CCXT.Simple.Data;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace CCXT.Simple.Exchanges.Huobi
{
    public class XHuobi : IExchange
    {
        /*
		 * HuobiGlobal Support Markets: usdt,btc
		 *
		 * Rate Limit
		 *     https://huobiapi.github.io/docs/spot/v1/en/#rate-limiting-rule
		 *     https://huobiapi.github.io/docs/spot/v1/en/#websocket-market-data
		 *     https://huobiglobal.zendesk.com/hc/en-us/articles/900001168066-Huobi-Global-is-going-to-change-rate-limit-policy-for-part-of-REST-API-endpoints
		 *
		 *     Order interface is limited by API Key: no more than 10 times within 1 sec
		 *     Market data interface is limited by IP: no more than 10 times within 1 sec
		 */

        public XHuobi(Exchange mainXchg, string apiKey = "", string secretKey = "", string passPhrase = "")
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

        public string ExchangeName { get; set; } = "huobi";

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
                    using HttpResponseMessage _response = await _wc.GetAsync("https://api.huobi.pro/v1/common/symbols");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_jstring);
                    var _jarray = _jobject["data"].ToObject<JArray>();

                    _queue_info.symbols.Clear();

                    foreach (JToken s in _jarray)
                    {
                        var _symbol_partition = s.Value<string>("symbol-partition");
                        if (_symbol_partition != "main")
                            continue;

                        var _quote_name = s.Value<string>("quote-currency");

                        if (_quote_name == "usdt" || _quote_name == "btc")
                        {
                            _queue_info.symbols.Add(new QueueSymbol
                            {
                                symbol = s.Value<string>("symbol"),
                                tickSize = s.Value<decimal>("price-precision"),
                                baseName = s.Value<string>("base-currency").ToUpper(),
                                quoteName = _quote_name.ToUpper()
                            });
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1911);
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
                    using HttpResponseMessage _response = await _wc.GetAsync("https://api.huobi.pro/v2/reference/currencies");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_jstring);

                    var _jarray = _jobject.Value<JArray>("data");
                    foreach (var s in _jarray)
                    {
                        var _wallet_state = s.Value<string>("instStatus");
                        if (_wallet_state != "normarl")
                            continue;

                        var _currency = s.Value<string>("currency");

                        var _state = states.states.SingleOrDefault(x => x.currency == _currency);
                        if (_state == null)
                        {
                            _state = new WState
                            {
                                currency = _currency,
                                active = true
                            };

                            states.states.Add(_state);
                        }

                        foreach (var n in s["chains"])
                        {
                            var _network = new WNetwork
                            {
                                name = n.Value<string>("chain"),
                                network = n.Value<string>("baseChain"),
                                protocol = n.Value<string>("baseChainProtocol"),

                                deposit = n.Value<string>("depositStatus") == "allowed",
                                withdraw = n.Value<string>("withdrawStatus") == "allowed",

                                withdrawFee = n.Value<decimal>("transactFeeWithdraw"),
                                minWithdrawal = n.Value<decimal>("minWithdrawAmt"),
                                maxWithdrawal = n.Value<decimal>("maxWithdrawAmt"),

                                minConfirm = n.Value<int>("numOfConfirmations")
                            };

                            _state.networks.Add(_network);
                        }
                    }
                }

                this.mainXchg.OnMessageEvent(ExchangeName, $"checking deposit & withdraw status...", 1912);
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1913);
            }
        }

        public async ValueTask<decimal> GetPrice(string symbol)
        {
            var _result = 0.0m;

            try
            {
                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _response = await _wc.GetAsync("");
                    var _jstring = await _response.Content.ReadAsStringAsync();

                    var _jobject = JObject.Parse(_jstring);


                    Debug.Assert(_result != 0.0m);
                }
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1914);
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
                await Task.Delay(100);

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1915);
            }

            return _result;
        }

        /// <summary>
        ///
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
                    using HttpResponseMessage _response = await _wc.GetAsync("https://api.huobi.pro/market/tickers");
                    var _tstring = await _response.Content.ReadAsStringAsync();
                    var _jstring = _tstring
                                        .Substring(9, _tstring.Length - 44)
                                        .Replace("\"symbol\":", "")
                                        .Replace("\",\"open\"", "\":{\"open\"")
                                        .Replace("},{", "},")
                                        + "}";

                    var _jobject = JObject.Parse(_jstring);

                    for (var i = 0; i < tickers.items.Count; i++)
                    {
                        var _ticker = tickers.items[i];
                        if (_ticker.symbol == "X")
                            continue;

                        if (_jobject.ContainsKey(_ticker.symbol))
                        {
                            var _price = _jobject[_ticker.symbol].Value<decimal>("close");
                            {
                                if (_ticker.quoteName == "USDT")
                                {
                                    _ticker.lastPrice = _price * tickers.exchgRate;

                                    _ticker.askPrice = _price * tickers.exchgRate;
                                    _ticker.bidPrice = _price * tickers.exchgRate;
                                }
                                else if (_ticker.quoteName == "BTC")
                                {
                                    _ticker.lastPrice = _price * mainXchg.btc_krw_price;

                                    _ticker.askPrice = _price * mainXchg.btc_krw_price;
                                    _ticker.bidPrice = _price * mainXchg.btc_krw_price;
                                }
                            }
                        }
                        else
                        {
                            this.mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 1916);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1917);
            }

            return _result;
        }

        /// <summary>
        /// Get Volumes
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
                    using HttpResponseMessage _response = await _wc.GetAsync("https://api.huobi.pro/market/tickers");
                    var _tstring = await _response.Content.ReadAsStringAsync();
                    var _jstring = _tstring
                                        .Substring(9, _tstring.Length - 44)
                                        .Replace("\"symbol\":", "")
                                        .Replace("\",\"open\"", "\":{\"open\"")
                                        .Replace("},{", "},")
                                        + "}";

                    var _jobject = JObject.Parse(_jstring);

                    for (var i = 0; i < tickers.items.Count; i++)
                    {
                        var _ticker = tickers.items[i];
                        if (_ticker.symbol == "X")
                            continue;

                        if (_jobject.ContainsKey(_ticker.symbol))
                        {
                            var _volume = _jobject[_ticker.symbol].Value<decimal>("vol");
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
                            this.mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 1918);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1919);
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
                    using HttpResponseMessage _response = await _wc.GetAsync("https://api.huobi.pro/market/tickers");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_jstring);
                    var _jdata = _jobject["data"].ToObject<JArray>();

                    for (var i = 0; i < tickers.items.Count; i++)
                    {
                        var _ticker = tickers.items[i];
                        if (_ticker.symbol == "X")
                            continue;

                        var _jitem = _jdata.SingleOrDefault(x => x["symbol"].ToString() == _ticker.symbol);
                        if (_jitem != null)
                        {
                            var _last_price = _jitem.Value<decimal>("close");
                            {
                                var _ask_price = _jitem.Value<decimal>("ask");
                                var _bid_price = _jitem.Value<decimal>("bid");

                                _ticker.askQty = _jitem.Value<decimal>("askSize");
                                _ticker.bidQty = _jitem.Value<decimal>("bidSize");

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

                            var _volume = _jitem.Value<decimal>("vol");
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
                            this.mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 1920);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1921);
            }

            return _result;
        }
    }
}