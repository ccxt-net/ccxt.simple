using CCXT.Simple.Base;
using CCXT.Simple.Data;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using System.Text;

namespace CCXT.Simple.Exchanges.Okex
{
    public class XOKEx : IExchange
    {
        /*
		 * OK-EX Support Markets: USDT, BTC
		 *
		 * REST API
		 *     https://www.okex.com/docs-v5/en/#market-maker-program
		 *
		 */

        public XOKEx(Exchange mainXchg, string apiKey, string secretKey, string passPharase)
        {
            this.mainXchg = mainXchg;

            this.ApiKey = apiKey;
            this.SecretKey = secretKey; 
            this.Passphrase = passPharase;
        }

        private Exchange mainXchg
        {
            get;
            set;
        }

        public string ExchangeName { get; set; } = "okex";

        public bool Alive
        {
            get;
            set;
        }

        private string ApiKey
        {
            get;
            set;
        }

        private string Passphrase
        {
            get;
            set;
        }

        private string SecretKey
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
                    using HttpResponseMessage _response = await _wc.GetAsync("https://www.okex.com/api/v5/public/instruments?instType=SPOT");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_jstring);
                    var _jarray = _jobject["data"].ToObject<JArray>();

                    _queue_info.symbols.Clear();

                    foreach (var s in _jarray)
                    {
                        var _quote_name = s.Value<string>("quoteCcy");

                        if (_quote_name == "USDT" || _quote_name == "BTC")
                        {
                            _queue_info.symbols.Add(new QueueSymbol
                            {
                                symbol = s.Value<string>("instId"),
                                tickSize = s.Value<decimal>("tickSz"),
                                baseName = s.Value<string>("baseCcy"),
                                quoteName = _quote_name
                            });
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 2211);
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
                    this.CreateSignature(_wc);

                    using HttpResponseMessage _response = await _wc.GetAsync("https://www.okex.com/api/v5/asset/currencies");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_jstring);
                    var _jarray = _jobject["data"].ToObject<JArray>();

                    foreach (var s in _jarray)
                    {
                        var _currency = s.Value<string>("ccy");

                        var _state = states.states.SingleOrDefault(x => x.currency == _currency);
                        if (_state == null)
                        {
                            _state = new WState
                            {
                                currency = _currency,
                                active = true,

                                deposit = s.Value<bool>("canDep"),
                                withdraw = s.Value<bool>("canWd"),

                                networks = new List<WNetwork>()
                            };

                            states.states.Add(_state);
                        }

                        _state.networks.Add(new WNetwork
                        {
                            name = s.Value<string>("chain"),
                            //network = s.Value<string>("chain"),
                            protocol = s.Value<string>("chain"),

                            deposit = s.Value<bool>("canDep"),
                            withdraw = s.Value<bool>("canWd"),

                            withdrawFee = s.Value<decimal>("maxFee"),
                            minWithdrawal = s.Value<decimal>("minWd"),
                            maxWithdrawal = s.Value<decimal>("maxWd"),

                            minConfirm = s.Value<int>("minDepArrivalConfirm")
                        });
                    }
                }

                this.mainXchg.OnMessageEvent(ExchangeName, $"checking deposit & withdraw status...", 2212);
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 2213);
            }
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

        private void CreateSignature(HttpClient client)
        {
            var _timestamp = CUnixTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'.'fff'Z'");

            var _post_data = $"{_timestamp}GET/api/v5/asset/currencies";
            var _signature = Convert.ToBase64String(Encryptor.ComputeHash(Encoding.UTF8.GetBytes(_post_data)));

            client.DefaultRequestHeaders.Add("USER-AGENT", mainXchg.UserAgent);
            client.DefaultRequestHeaders.Add("OK-ACCESS-KEY", this.ApiKey);
            client.DefaultRequestHeaders.Add("OK-ACCESS-SIGN", _signature);
            client.DefaultRequestHeaders.Add("OK-ACCESS-TIMESTAMP", _timestamp);
            client.DefaultRequestHeaders.Add("OK-ACCESS-PASSPHRASE", this.Passphrase);
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
                    using HttpResponseMessage _response = await _wc.GetAsync("https://www.okex.com/api/v5/market/ticker?instId=" + symbol);
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_jstring);

                    var _jdata = _jobject["data"].ToObject<JArray>();
                    _result = _jobject[0].Value<decimal>("last");
                }
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 2214);
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
                    using HttpResponseMessage _response = await _wc.GetAsync("https://www.okex.com/api/v5/market/tickers?instType=SPOT");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_jstring);

                    var _jdata = _jobject["data"].ToObject<JArray>();

                    for (var i = 0; i < tickers.items.Count; i++)
                    {
                        var _ticker = tickers.items[i];
                        if (_ticker.symbol == "X")
                            continue;

                        var _jitem = _jdata.SingleOrDefault(x => x["instId"].ToString() == _ticker.symbol);
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
                            this.mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 2215);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 2216);
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
                    using HttpResponseMessage _response = await _wc.GetAsync("https://www.okex.com/api/v5/market/tickers?instType=SPOT");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_jstring);

                    var _jdata = _jobject["data"].ToObject<JArray>();

                    for (var i = 0; i < tickers.items.Count; i++)
                    {
                        var _ticker = tickers.items[i];
                        if (_ticker.symbol == "X")
                            continue;

                        var _jitem = _jdata.SingleOrDefault(x => x["instId"].ToString() == _ticker.symbol);
                        if (_jitem != null)
                        {
                            var _last_price = _jitem.Value<decimal>("last");
                            {
                                var _ask_price = _jitem.Value<decimal>("askPx");
                                var _bid_price = _jitem.Value<decimal>("bidPx");

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

                                _ticker.askQty = _jitem.Value<decimal>("askSz");
                                _ticker.bidQty = _jitem.Value<decimal>("bidSz");
                            }
                        }
                        else
                        {
                            this.mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 2217);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 2218);
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
                    using HttpResponseMessage _response = await _wc.GetAsync("https://www.okex.com/api/v5/market/tickers?instType=SPOT");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_jstring);

                    var _jdata = _jobject["data"].ToObject<JArray>();

                    for (var i = 0; i < tickers.items.Count; i++)
                    {
                        var _ticker = tickers.items[i];
                        if (_ticker.symbol == "X")
                            continue;

                        var _jitem = _jdata.SingleOrDefault(x => x["instId"].ToString() == _ticker.symbol);
                        if (_jitem != null)
                        {
                            var _volume = _jitem.Value<decimal>("volCcy24h");
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
                            this.mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 2219);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 2220);
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
                    using HttpResponseMessage _response = await _wc.GetAsync("https://www.okex.com/api/v5/market/tickers?instType=SPOT");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_jstring);

                    var _jdata = _jobject["data"].ToObject<JArray>();

                    for (var i = 0; i < tickers.items.Count; i++)
                    {
                        var _ticker = tickers.items[i];
                        if (_ticker.symbol == "X")
                            continue;

                        var _jitem = _jdata.SingleOrDefault(x => x["instId"].ToString() == _ticker.symbol);
                        if (_jitem != null)
                        {
                            var _last_price = _jitem.Value<decimal>("last");
                            {
                                var _ask_price = _jitem.Value<decimal>("askPx");
                                var _bid_price = _jitem.Value<decimal>("bidPx");

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

                                _ticker.askQty = _jitem.Value<decimal>("askSz");
                                _ticker.bidQty = _jitem.Value<decimal>("bidSz");
                            }

                            var _volume = _jitem.Value<decimal>("volCcy24h");
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
                            this.mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 2221);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 2222);
            }

            return _result;
        }
    }
}