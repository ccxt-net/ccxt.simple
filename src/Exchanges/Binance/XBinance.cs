using CCXT.Simple.Base;
using CCXT.Simple.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace CCXT.Simple.Exchanges.Binance
{
    public class XBinance : IExchange
    {
        /*
		 * Binance Support Markets: USDT,BUSD,BTC
		 *
		 * https://github.com/binance/binance-spot-api-docs/blob/master/rest-api.md
		 * https://github.com/binance/binance-spot-api-docs/blob/master/web-socket-streams.md
		 *
		 * Rate Limit
		 *     https://python-binance.readthedocs.io/en/latest/binance.html#binance.client.Client.get_exchange_info
		 *
		 * {
		 *     "timezone": "UTC",
		 *     "serverTime": 1508631584636,
		 *     "rateLimits": [
		 *         {
		 *             "rateLimitType": "REQUESTS",
		 *             "interval": "MINUTE",
		 *             "limit": 1200
		 *         },
		 *         {
		 *             "rateLimitType": "ORDERS",
		 *             "interval": "SECOND",
		 *             "limit": 10
		 *         },
		 *         {
		 *             "rateLimitType": "ORDERS",
		 *             "interval": "DAY",
		 *             "limit": 100000
		 *         }
		 *     ]
		 * }
		 */

        public XBinance(Exchange mainXchg, string apiKey = "", string secretKey = "", string passPhrase = "")
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

        public string ExchangeName { get; set; } = "binance";

        public string ExchangeUrl { get; set; } = "https://api.binance.com";

        public bool Alive { get; set; }
        public string ApiKey { get; set; }
        public string SecretKey { get; set; }
        public string PassPhrase { get; set; }
        public Tickers Tickers { get; set; }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public async ValueTask<bool> VerifySymbols()
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
                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrl}/api/v3/ticker/price");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jarray = JsonConvert.DeserializeObject<List<Market>>(_jstring);

                    _queue_info.symbols.Clear();

                    foreach (var s in _jarray)
                    {
                        var _symbol = s.symbol;

                        if (_symbol.EndsWith("USDT") || _symbol.EndsWith("BUSD") || _symbol.EndsWith("BTC"))
                        {
                            var _len = _symbol.EndsWith("BTC") ? 3 : 4;
                            var _base = _symbol.Substring(0, _symbol.Length - _len);
                            var _quote = _symbol.Substring(_symbol.Length - _len);

                            _queue_info.symbols.Add(new QueueSymbol
                            {
                                symbol = _symbol,
                                compName = _base,
                                baseName = _base,
                                quoteName = _quote
                            });
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1115);
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
        public async ValueTask<bool> VerifyStates(Tickers tickers)
        {
            var _result = false;

            try
            {
                using (var _wc = new HttpClient())
                {
                    var _args = this.CreateSignature(_wc);

                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrl}/sapi/v1/capital/config/getall?" + _args);
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jarray = JsonConvert.DeserializeObject<List<CoinInfor>>(_jstring);

                    foreach (var c in _jarray)
                    {
                        var _state = tickers.states.SingleOrDefault(x => x.currency == c.coin);
                        if (_state == null)
                        {
                            _state = new WState
                            {
                                currency = c.coin,
                                active = c.trading,
                                deposit = c.depositAllEnable,
                                withdraw = c.withdrawAllEnable,
                                networks = new List<WNetwork>()
                            };

                            tickers.states.Add(_state);
                        }
                        else
                        {
                            _state.active = c.trading;
                            _state.deposit = c.depositAllEnable;
                            _state.withdraw = c.withdrawAllEnable;
                        }

                        var _t_items = tickers.items.Where(x => x.baseName == _state.currency);
                        if (_t_items != null)
                        {
                            foreach (var t in _t_items)
                            {
                                t.active = _state.active;
                                t.deposit = _state.deposit;
                                t.withdraw = _state.withdraw;
                            }
                        }

                        foreach (var n in c.networkList)
                        {
                            var _name = n.coin + "-" + n.network;

                            var _network = _state.networks.SingleOrDefault(x => x.name == _name);
                            if (_network == null)
                            {
                                var _protocol = n.network;
                                if (n.network == "ETH" && n.name.Contains("ERC20"))
                                    _protocol = "ERC20";

                                _network = new WNetwork
                                {
                                    name = _name,
                                    network = n.network,
                                    protocol = _protocol,

                                    deposit = n.depositEnable,
                                    withdraw = n.withdrawEnable,

                                    withdrawFee = n.withdrawFee,
                                    minWithdrawal = n.withdrawMin,
                                    maxWithdrawal = n.withdrawMax,

                                    minConfirm = n.minConfirm,
                                    arrivalTime = n.estimatedArrivalTime
                                };

                                _state.networks.Add(_network);
                            }
                            else
                            {
                                _network.deposit = n.depositEnable;
                                _network.withdraw = n.withdrawEnable;
                            }
                        }
                    }

                    _result = true;
                }

                this.mainXchg.OnMessageEvent(ExchangeName, $"checking deposit & withdraw status...", 1116);
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1117);
            }

            return _result;
        }

        private HMACSHA256 __encryptor = null;

        /// <summary>
        ///
        /// </summary>
        public HMACSHA256 Encryptor
        {
            get
            {
                if (__encryptor == null)
                    __encryptor = new HMACSHA256(Encoding.UTF8.GetBytes(this.SecretKey));

                return __encryptor;
            }
        }

        private string CreateSignature(HttpClient client)
        {
            client.DefaultRequestHeaders.Add("USER-AGENT", mainXchg.UserAgent);
            client.DefaultRequestHeaders.Add("X-MBX-APIKEY", this.ApiKey);

            var _post_data = $"timestamp={CUnixTime.NowMilli}";
            var _signature = BitConverter.ToString(Encryptor.ComputeHash(Encoding.UTF8.GetBytes(_post_data))).Replace("-", "");

            return _post_data + $"&signature={_signature}";
        }

        /// <summary>
        /// Get Binance BTCUSDT Last Price
        /// </summary>
        /// <returns></returns>
        public async ValueTask<decimal> GetPrice(string symbol)
        {
            var _result = 0.0m;

            try
            {
                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrl}/api/v3/ticker/24hr?symbol=" + symbol);
                    var _jstring = await _response.Content.ReadAsStringAsync();

                    var _jobject = JObject.Parse(_jstring);
                    _result = _jobject.Value<decimal>("lastPrice");

                    Debug.Assert(_result != 0.0m);
                }
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1118);
            }

            return _result;
        }

        public async ValueTask<bool> GetTickers(Tickers tickers)
        {
            var _result = false;

            try
            {
                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrl}/api/v3/ticker/24hr");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jtickers = JsonConvert.DeserializeObject<List<RaTicker>>(_jstring);

                    for (var i = 0; i < tickers.items.Count; i++)
                    {
                        var _ticker = tickers.items[i];
                        if (_ticker.symbol == "X")
                            continue;

                        var _jticker = _jtickers.Where(x => x.symbol == _ticker.symbol).FirstOrDefault();
                        if (_jticker != null)
                        {
                            if (_ticker.quoteName == "USDT" || _ticker.quoteName == "BUSD")
                            {
                                if (_ticker.symbol == "BTCUSDT")
                                    this.mainXchg.OnUsdPriceEvent(_jticker.lastPrice);

                                _ticker.lastPrice = _jticker.lastPrice * tickers.exchgRate;
                            }
                            else if (_ticker.quoteName == "BTC")
                            {
                                _ticker.lastPrice = _jticker.lastPrice * mainXchg.btc_krw_price;
                            }
                        }
                        else
                        {
                            this.mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 1119);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1120);
            }

            return _result;
        }

        /// <summary>
        /// Get Binance Tickers
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
                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrl}/api/v3/ticker/bookTicker");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _tickers = JsonConvert.DeserializeObject<List<BookTicker>>(_jstring);

                    for (var i = 0; i < tickers.items.Count; i++)
                    {
                        var _ticker = tickers.items[i];
                        if (_ticker.symbol == "X")
                            continue;

                        var _jticker = _tickers.Where(x => x.symbol == _ticker.symbol).FirstOrDefault();
                        if (_jticker != null)
                        {
                            if (_ticker.quoteName == "USDT" || _ticker.quoteName == "BUSD")
                            {
                                _ticker.askPrice = _jticker.askPrice * tickers.exchgRate;
                                _ticker.bidPrice = _jticker.bidPrice * tickers.exchgRate;
                            }
                            else if (_ticker.quoteName == "BTC")
                            {
                                _ticker.askPrice = _jticker.askPrice * mainXchg.btc_krw_price;
                                _ticker.bidPrice = _jticker.bidPrice * mainXchg.btc_krw_price;
                            }

                            _ticker.askQty = _jticker.askQty;
                            _ticker.bidQty = _jticker.bidQty;
                        }
                        else
                        {
                            this.mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 1121);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1122);
            }

            return _result;
        }

        /// <summary>
        /// Get Binance Volumes
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
                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrl}/api/v3/ticker/24hr");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jtickers = JsonConvert.DeserializeObject<List<RaTicker>>(_jstring);

                    for (var i = 0; i < tickers.items.Count; i++)
                    {
                        var _ticker = tickers.items[i];
                        if (_ticker.symbol == "X")
                            continue;

                        var _jitem = _jtickers.SingleOrDefault(x => x.symbol == _ticker.symbol);
                        if (_jitem != null)
                        {
                            var _volume = _jitem.quoteVolume;
                            {
                                var _prev_volume24h = _ticker.previous24h;
                                var _next_timestamp = _ticker.timestamp + 60 * 1000;

                                if (_ticker.quoteName == "USDT" || _ticker.quoteName == "BUSD")
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
                            this.mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 1123);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1124);
            }

            return _result;
        }

        /// <summary>
        /// Get Binance Markets
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
                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrl}/api/v3/ticker/24hr");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jtickers = JsonConvert.DeserializeObject<List<RaTicker>>(_jstring);

                    for (var i = 0; i < tickers.items.Count; i++)
                    {
                        var _ticker = tickers.items[i];
                        if (_ticker.symbol == "X")
                            continue;

                        var _jticker = _jtickers.Where(x => x.symbol == _ticker.symbol).FirstOrDefault();
                        if (_jticker != null)
                        {
                            var _last_price = _jticker.lastPrice;
                            {
                                if (_ticker.quoteName == "USDT" || _ticker.quoteName == "BUSD")
                                {
                                    if (_ticker.symbol == "BTCUSDT")
                                        this.mainXchg.OnUsdPriceEvent(_last_price);

                                    _ticker.lastPrice = _last_price * tickers.exchgRate;

                                    _ticker.askPrice = _jticker.askPrice * tickers.exchgRate;
                                    _ticker.bidPrice = _jticker.bidPrice * tickers.exchgRate;
                                }
                                else if (_ticker.quoteName == "BTC")
                                {
                                    _ticker.lastPrice = _last_price * mainXchg.btc_krw_price;

                                    _ticker.askPrice = _jticker.askPrice * mainXchg.btc_krw_price;
                                    _ticker.bidPrice = _jticker.bidPrice * mainXchg.btc_krw_price;
                                }

                                _ticker.askQty = _jticker.askQty;
                                _ticker.bidQty = _jticker.bidQty;
                            }

                            var _volume = _jticker.quoteVolume;
                            {
                                var _prev_volume24h = _ticker.previous24h;
                                var _next_timestamp = _ticker.timestamp + 60 * 1000;

                                if (_ticker.quoteName == "USDT" || _ticker.quoteName == "BUSD")
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
                            this.mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 1125);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1126);
            }

            return _result;
        }
    }
}