﻿using CCXT.Simple.Base;
using CCXT.Simple.Data;
using Newtonsoft.Json;
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

        public string ExchangeUrl { get; set; } = "https://api.huobi.pro";

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
                using (var _client = new HttpClient())
                {
                    var _response = await _client.GetAsync($"{ExchangeUrl}/v1/common/symbols");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_jstring);
                    var _jarray = _jobject["data"].ToObject<JArray>();

                    var _queue_info = mainXchg.GetXInfors(ExchangeName);

                    foreach (JToken s in _jarray)
                    {
                        var _symbol_partition = s.Value<string>("symbol-partition");
                        if (_symbol_partition != "main")
                            continue;

                        var _base = s.Value<string>("base-currency");
                        var _quote = s.Value<string>("quote-currency");

                        if (_quote == "usdt" || _quote == "btc")
                        {
                            _queue_info.symbols.Add(new QueueSymbol
                            {
                                symbol = s.Value<string>("symbol"),
                                compName = _base.ToUpper(),
                                baseName = _base.ToUpper(),
                                quoteName = _quote.ToUpper(),
                                tickSize = s.Value<decimal>("price-precision")
                            });
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                mainXchg.OnMessageEvent(ExchangeName, ex, 3801);
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
                using (var _client = new HttpClient())
                {
                    var _response = await _client.GetAsync($"{ExchangeUrl}/v2/reference/currencies");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jarray = JsonConvert.DeserializeObject<CoinInfor>(_jstring);

                    foreach (var c in _jarray.data)
                    {
                        var _base_name = c.currency.ToUpper();

                        var _state = tickers.states.SingleOrDefault(x => x.baseName == _base_name);
                        if (_state == null)
                        {
                            _state = new WState
                            {
                                baseName = _base_name,
                                active = c.instStatus == "normal",
                                deposit = true,
                                withdraw = true,
                                networks = new List<WNetwork>()
                            };

                            tickers.states.Add(_state);
                        }
                        else
                        {
                            _state.active = c.instStatus == "normal";
                        }

                        var _t_items = tickers.items.Where(x => x.compName == _state.baseName);
                        if (_t_items != null)
                        {
                            foreach (var t in _t_items)
                            {
                                t.active = _state.active;
                                t.deposit = _state.deposit;
                                t.withdraw = _state.withdraw;
                            }
                        }

                        foreach (var n in c.chains)
                        {
                            var _name = n.chain;

                            var _network = _state.networks.SingleOrDefault(x => x.name == _name);
                            if (_network == null)
                            {
                                _network = new WNetwork
                                {
                                    name = _name,
                                    network = n.baseChain.IsNotEmpty(n.displayName).ToUpper(),
                                    chain = n.baseChainProtocol.IsNotEmpty(n.displayName).ToUpper(),

                                    deposit = n.depositStatus == "allowed",
                                    withdraw = n.withdrawStatus == "allowed",

                                    withdrawFee = n.transactFeeWithdraw,
                                    minWithdrawal = n.minWithdrawAmt,
                                    maxWithdrawal = n.maxWithdrawAmt,

                                    minConfirm = n.numOfConfirmations
                                };

                                _state.networks.Add(_network);
                            }
                            else
                            {
                                _network.deposit = n.depositStatus == "allowed";
                                _network.withdraw = n.withdrawStatus == "allowed";
                            }
                        }
                    }

                    _result = true;
                }

                mainXchg.OnMessageEvent(ExchangeName, $"checking deposit & withdraw status...", 3802);
            }
            catch (Exception ex)
            {
                mainXchg.OnMessageEvent(ExchangeName, ex, 3803);
            }

            return _result;
        }

        public async ValueTask<decimal> GetPrice(string symbol)
        {
            var _result = 0.0m;

            try
            {
                using (var _client = new HttpClient())
                {
                    var _response = await _client.GetAsync("");
                    var _jstring = await _response.Content.ReadAsStringAsync();

                    var _jobject = JObject.Parse(_jstring);


                    Debug.Assert(_result != 0.0m);
                }
            }
            catch (Exception ex)
            {
                mainXchg.OnMessageEvent(ExchangeName, ex, 3804);
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
                mainXchg.OnMessageEvent(ExchangeName, ex, 3805);
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
                using (var _client = new HttpClient())
                {
                    var _response = await _client.GetAsync($"{ExchangeUrl}/market/tickers");
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
                                    _ticker.lastPrice = _price * mainXchg.fiat_btc_price;

                                    _ticker.askPrice = _price * mainXchg.fiat_btc_price;
                                    _ticker.bidPrice = _price * mainXchg.fiat_btc_price;
                                }
                            }
                        }
                        else
                        {
                            mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 3806);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                mainXchg.OnMessageEvent(ExchangeName, ex, 3807);
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
                using (var _client = new HttpClient())
                {
                    var _response = await _client.GetAsync($"{ExchangeUrl}/market/tickers");
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
                                    _volume *= mainXchg.fiat_btc_price;

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
                            mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 3808);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                mainXchg.OnMessageEvent(ExchangeName, ex, 3809);
            }

            return _result;
        }

        public async ValueTask<bool> GetMarkets(Tickers tickers)
        {
            var _result = false;

            try
            {
                using (var _client = new HttpClient())
                {
                    var _response = await _client.GetAsync($"{ExchangeUrl}/market/tickers");
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
                                    _ticker.lastPrice = _last_price * mainXchg.fiat_btc_price;

                                    _ticker.askPrice = _ask_price * mainXchg.fiat_btc_price;
                                    _ticker.bidPrice = _bid_price * mainXchg.fiat_btc_price;
                                }
                            }

                            var _volume = _jitem.Value<decimal>("vol");
                            {
                                var _prev_volume24h = _ticker.previous24h;
                                var _next_timestamp = _ticker.timestamp + 60 * 1000;

                                if (_ticker.quoteName == "USDT")
                                    _volume *= tickers.exchgRate;
                                else if (_ticker.quoteName == "BTC")
                                    _volume *= mainXchg.fiat_btc_price;

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
                            mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 3810);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                mainXchg.OnMessageEvent(ExchangeName, ex, 3811);
            }

            return _result;
        }
    }
}