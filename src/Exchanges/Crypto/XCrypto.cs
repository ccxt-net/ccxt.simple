﻿using CCXT.Simple.Base;
using CCXT.Simple.Data;
using CCXT.Simple.Exchanges.Bithumb;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

namespace CCXT.Simple.Exchanges.Crypto
{
    public class XCrypto : IExchange
    {
        /*
		 * Crypto Support Markets: USDT, USDC, BTC
		 *
		 * REST API
		 *     https://docs.crypto.com/#general
		 *     https://docs.crypto.com/#symbol-snapshot
		 *
		 */

        public XCrypto(Exchange mainXchg, string apiKey = "", string secretKey = "", string passPhrase = "")
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

        public string ExchangeName { get; set; } = "crypto";

        public string ExchangeUrl { get; set; } = "https://api.crypto.com";

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
                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrl}/v2/public/get-instruments");
                    if (_response.IsSuccessStatusCode)
                    {
                        var _jstring = await _response.Content.ReadAsStringAsync();
                        var _jarray = JsonConvert.DeserializeObject<Market>(_jstring);

                        var _queue_info = mainXchg.GetXInfors(ExchangeName);

                        foreach (var s in _jarray.result.instruments)
                        {
                            s.quote_currency = s.quote_currency.Split('_')[0].Split(' ')[0];

                            if (s.quote_currency == "USDT" || s.quote_currency == "USD" || s.quote_currency == "BTC")
                            {
                                _queue_info.symbols.Add(new QueueSymbol
                                {
                                    symbol = s.instrument_name,
                                    compName = s.base_currency,
                                    baseName = s.base_currency,
                                    quoteName = s.quote_currency,
                                    tickSize = s.min_quantity
                                });
                            }
                        }

                        _result = true;
                    }
                }
            }
            catch (Exception ex)
            {
                mainXchg.OnMessageEvent(ExchangeName, ex, 3601);
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
                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrl}/v2/public/get-ticker");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jticker = JsonConvert.DeserializeObject<RaTickers>(_jstring, mainXchg.JsonSettings);

                    if (_jticker.code == 0)
                    {
                        var _jdata = _jticker.result.data;

                        for (var i = 0; i < tickers.items.Count; i++)
                        {
                            var _ticker = tickers.items[i];
                            if (_ticker.symbol == "X")
                                continue;

                            var _jitem = _jdata.SingleOrDefault(x => x.i == _ticker.symbol);
                            if (_jitem != null)
                            {
                                var _last_price = _jitem.a;
                                {
                                    var _ask_price = _jitem.k;
                                    var _bid_price = _jitem.b;

                                    if (_ticker.quoteName == "USDT" || _ticker.quoteName == "USD")
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

                                var _volume = _jitem.vv;      // The total 24h traded volume value (in USD)
                                {
                                    var _prev_volume24h = _ticker.previous24h;
                                    var _next_timestamp = _ticker.timestamp + 60 * 1000;

                                    _volume *= tickers.exchgRate;
                                    _ticker.volume24h = Math.Floor(_volume / mainXchg.Volume24hBase);

                                    var _curr_timestamp = _jitem.t;
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
                                mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 3602);
                                _ticker.symbol = "X";
                            }
                        }

                        _result = true;
                    }
                }
            }
            catch (Exception ex)
            {
                mainXchg.OnMessageEvent(ExchangeName, ex, 3603);
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

        public Request CreateSignature(HttpClient client, int id, string endpoint, Dictionary<string, string> args = null)
        {
            if (args == null)
                args = new Dictionary<string, string>();

            var _post_data = args.ToQueryString2();
            var _nonce = CUnixTime.NowMilli;

            var _sign_data = $"{endpoint}{id}{this.ApiKey}{_post_data}{_nonce}";
            var _sign_hash = Encryptor.ComputeHash(Encoding.UTF8.GetBytes(_sign_data));

            var _sign = Convert.ToHexString(_sign_hash).ToLower();

            return new Request
            { 
                id = id,
                api_key = this.ApiKey,
                method = endpoint,
                nonce = _nonce,
                @params = args,
                sig = _sign
            };
        }

        public async ValueTask<bool> VerifyStates(Tickers tickers)
        {
            var _result = false;

            try
            {
                using (var _client = new HttpClient())
                {
                    var _endpoint = "private/get-currency-networks";
                    var _request = this.CreateSignature(_client, 1, _endpoint);

                    var _response = await _client.PostAsJsonAsync($"{ExchangeUrl}/v2/{_endpoint}", _request);
                    if (_response.IsSuccessStatusCode)
                    {
                        var _jstring = await _response.Content.ReadAsStringAsync();
                        var _jarray = JsonConvert.DeserializeObject<CoinState>(_jstring, mainXchg.JsonSettings);

                        foreach (var m in _jarray.result.currency_map)
                        {
                            var _base_name = m.Key;
                            var _c = JsonConvert.DeserializeObject<CurrencyMap>(m.Value.ToString());

                            var _state = tickers.states.SingleOrDefault(x => x.baseName == _base_name);
                            if (_state == null)
                            {
                                _state = new WState
                                {
                                    baseName = _base_name,
                                    active = true,
                                    deposit = true,
                                    withdraw = true,
                                    networks = new List<WNetwork>()
                                };

                                tickers.states.Add(_state);
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

                            foreach (var n in _c.network_list)
                            {
                                var _name = _base_name + "-" + n.network_id;

                                var _network = _state.networks.SingleOrDefault(x => x.name == _name);
                                if (_network == null)
                                {
                                    var _protocol = n.network_id;
                                    if (_protocol == "ETH")
                                        _protocol = "ERC20";

                                    _network = new WNetwork
                                    {
                                        name = _name,
                                        network = n.network_id,
                                        chain = _protocol,

                                        deposit = n.deposit_enabled,
                                        withdraw = n.withdraw_enabled,

                                        withdrawFee = n.withdrawal_fee,
                                        minWithdrawal = n.min_withdrawal_amount,

                                        minConfirm = n.confirmation_required
                                    };

                                    _state.networks.Add(_network);
                                }
                                else
                                {
                                    _network.deposit = n.deposit_enabled;
                                    _network.withdraw = n.withdraw_enabled;
                                }
                            }
                        }
                    }

                    _result = true;
                }

                mainXchg.OnMessageEvent(ExchangeName, $"checking deposit & withdraw status...", 3604);
            }
            catch (Exception ex)
            {
                mainXchg.OnMessageEvent(ExchangeName, ex, 3605);
            }

            return _result;
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