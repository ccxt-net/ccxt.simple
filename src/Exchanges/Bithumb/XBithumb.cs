﻿using CCXT.Simple.Base;
using CCXT.Simple.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;

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

        public XBithumb(Exchange mainXchg, string apiKey = "", string secretKey = "", string passPhrase = "")
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

        public string ExchangeName { get; set; } = "bithumb";

        public string ExchangeUrl { get; set; } = "https://api.bithumb.com";

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
                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _k_response = await _wc.GetAsync($"{ExchangeUrl}/public/ticker/ALL_KRW");
                    var _k_jstring = await _k_response.Content.ReadAsStringAsync();
                    var _k_jobject = JObject.Parse(_k_jstring);

                    var _queue_info = this.mainXchg.GetQInfors(ExchangeName);

                    foreach (JProperty s in _k_jobject["data"].Children())
                    {
                       var _o = s.Value;
                        if (_o.Type != JTokenType.Object || !((JObject)_o).ContainsKey("opening_price"))
                            continue;

                        var _base = s.Name;

                        _queue_info.symbols.Add(new QueueSymbol
                        {
                            symbol = $"{_base}_KRW",
                            compName = _base,
                            baseName = _base,
                            quoteName = "KRW"
                        });
                    }

                    using HttpResponseMessage _b_response = await _wc.GetAsync($"{ExchangeUrl}/public/ticker/ALL_BTC");
                    var _b_jstring = await _b_response.Content.ReadAsStringAsync();
                    var _b_jobject = JObject.Parse(_b_jstring);

                    foreach (JProperty s in _b_jobject["data"].Children())
                    {
                        var _o = s.Value;
                        if (_o.Type != JTokenType.Object || !((JObject)_o).ContainsKey("opening_price"))
                            continue;

                        var _base = s.Name;

                        _queue_info.symbols.Add(new QueueSymbol
                        {
                            symbol = $"{_base}_BTC",
                            compName = _base,
                            baseName = _base,
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
        public async ValueTask<bool> VerifyStates(Tickers tickers)
        {
            var _result = false;

            try
            {
                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrl}/public/assetsstatus/ALL");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_jstring);

                    var _jdata = _jobject["data"];

                    foreach (JProperty s in _jdata.Children())
                    {
                        var _currency = s.Name;

                        var _state = tickers.states.SingleOrDefault(x => x.currency == _currency);
                        if (_state == null)
                        {
                            _state = new WState
                            {
                                currency = _currency,
                                active = true,
                                deposit = s.Value.Value<int>("deposit_status") > 0,
                                withdraw = s.Value.Value<int>("withdrawal_status") > 0,
                                networks = new List<WNetwork>()
                            };

                            tickers.states.Add(_state);
                        }
                        else
                        {
                            _state.deposit = s.Value.Value<int>("deposit_status") > 0;
                            _state.withdraw = s.Value.Value<int>("withdrawal_status") > 0;
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
                    }

                    _result = true;
                }

                this.mainXchg.OnMessageEvent(ExchangeName, $"checking deposit & withdraw status...", 1212);
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1213);
            }

            return _result;
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
                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrl}/public/ticker/" + symbol);
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

        public async ValueTask<Bithumb.RaOrderbook> GetOrderbook(string symbol)
        {
            var _result = new Bithumb.RaOrderbook();

            try
            {
                using (var _wc = new HttpClient())
                {
                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrl}/public/orderbook/" + symbol + "?count=30");
                    var _tstring = await _response.Content.ReadAsStringAsync();
                    var _jobject = JObject.Parse(_tstring);

                    var _asks = _jobject["data"].Value<JArray>("asks");
                    _result.asks.AddRange(
                        _asks.Select(x => new Bithumb.RaOrderbookItem
                        {
                            price = x.Value<decimal>("price"),
                            quantity = x.Value<decimal>("quantity")
                        })
                    );

                    var _bids = _jobject["data"].Value<JArray>("bids");
                    _result.bids.AddRange(
                        _bids.Select(x => new Bithumb.RaOrderbookItem
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
                    using HttpResponseMessage _k_response = await _wc.GetAsync($"{ExchangeUrl}/public/orderbook/ALL_KRW?count=1");
                    var _k_jstring = await _k_response.Content.ReadAsStringAsync();
                    var _k_jobject = JObject.Parse(_k_jstring);
                    var _k_data = _k_jobject["data"].ToObject<JObject>();

                    await Task.Delay(100);

                    using HttpResponseMessage _b_response = await _wc.GetAsync($"{ExchangeUrl}/public/orderbook/ALL_BTC?count=1");
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

                            Debug.Assert(this.mainXchg.krw_btc_price != 0.0m);

                            _ticker.askPrice = _ask.Value<decimal>("price") * mainXchg.krw_btc_price;
                            _ticker.askQty = _ask.Value<decimal>("quantity");
                            _ticker.bidPrice = _bid.Value<decimal>("price") * mainXchg.krw_btc_price;
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
                    using HttpResponseMessage _k_response = await _wc.GetAsync($"{ExchangeUrl}/public/ticker/ALL_KRW");
                    var _k_tstring = await _k_response.Content.ReadAsStringAsync();
                    var _k_jstring = _k_tstring.Substring(24, _k_tstring.Length - 25);
                    var _k_jobject = JObject.Parse(_k_jstring);

                    await Task.Delay(100);

                    using HttpResponseMessage _b_response = await _wc.GetAsync($"{ExchangeUrl}/public/ticker/ALL_BTC");
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
                            _ticker.lastPrice = _price * mainXchg.krw_btc_price;

                            var _volume = _b_jobject[_pairs[0]].Value<decimal>("acc_trade_value");
                            {
                                var _prev_volume24h = _ticker.previous24h;
                                var _next_timestamp = _ticker.timestamp + 60 * 1000;

                                _volume *= mainXchg.krw_btc_price;
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

        private HMACSHA512 __encryptor = null;

        /// <summary>
        ///
        /// </summary>
        public HMACSHA512 Encryptor
        {
            get
            {
                if (__encryptor == null)
                    __encryptor = new HMACSHA512(Encoding.UTF8.GetBytes(this.SecretKey));

                return __encryptor;
            }
        }

        private FormUrlEncodedContent CreateSignature(HttpClient client, string endpoint, Dictionary<string, string> args)
        {
            var _post_data = mainXchg.ToQueryString2(args);
            var _nonce = CUnixTime.NowMilli.ToString();

            var _sign_data = $"{endpoint};{_post_data};{_nonce}";
            var _sign_hash = Encryptor.ComputeHash(Encoding.UTF8.GetBytes(_sign_data));

            var _signature = Convert.ToBase64String(Encoding.UTF8.GetBytes(mainXchg.ConvertHexString(_sign_hash).ToLower()));
         
            client.DefaultRequestHeaders.Add("api-client-type", "2");
            client.DefaultRequestHeaders.Add("Api-Sign", _signature);
            client.DefaultRequestHeaders.Add("Api-Nonce", _nonce);
            client.DefaultRequestHeaders.Add("Api-Key", ApiKey);

            return new FormUrlEncodedContent(args);
        }
        
        private (bool success, string message) ParsingResponse(string jstring)
        {
            var _result = (success: false, message: "");

            var _json_result = JsonConvert.DeserializeObject<JToken>(jstring);

            var _json_status = _json_result.SelectToken("status");
            if (_json_status != null)
            {
                var _status_code = _json_status.Value<int>();
                if (_status_code != 0)
                {
                    var _json_message = _json_result.SelectToken("message");
                    if (_json_message != null)
                        _result.message = _json_message.Value<string>();
                }
                else
                    _result.success = true;
            }

            return _result;
        }

        public async ValueTask<(bool success, string message, string orderId)> CreateLimitOrderAsync(string base_name, string quote_name, decimal quantity, decimal price, SideType sideType)
        {
            var _result = (success: false, message: "", orderId: "");

            try
            {
                using (var _client = new HttpClient())
                {
                    var _endpoint = "/trade/place";

                    var _args = new Dictionary<string, string>();
                    {
                        _args.Add("endpoint", _endpoint);
                        _args.Add("order_currency", base_name);
                        _args.Add("payment_currency", quote_name);
                        _args.Add("units", $"{quantity}");
                        _args.Add("price", $"{price}");
                        _args.Add("type", sideType == SideType.Bid ? "bid" : "ask");
                    }

                    var _content = this.CreateSignature(_client, _endpoint, _args);

                    var _response = await _client.PostAsync($"{ExchangeUrl}{_endpoint}", _content);
                    if (_response.StatusCode == HttpStatusCode.OK)
                    {
                        var _jstring = await _response.Content.ReadAsStringAsync();
                        
                        var _json_result = this.ParsingResponse(_jstring);
                        if (_json_result.success)
                        {
                            var _json_data = JsonConvert.DeserializeObject<PlaceOrders>(_jstring);
                            if (_json_data.success)
                            {
                                _result.orderId = _json_data.orderId;
                                _result.success = true;
                            }
                        }
                        else
                        {
                            _result.message = _json_result.message;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 1219);
            }

            return _result;
        }
    }
}