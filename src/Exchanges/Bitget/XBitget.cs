﻿using CCXT.Simple.Base;
using CCXT.Simple.Data;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;

namespace CCXT.Simple.Exchanges.Bitget
{
    public class XBitget : IExchange
    {
        /*
		 * Bitget Support Markets: USDT,USDC,BTC,ETH,BRL
		 *
		 * https://bitgetlimited.github.io/apidoc/en/spot/#introduction
		 * 
		 */

        public XBitget(Exchange mainXchg, string apiKey = "", string secretKey = "", string passPhrase = "")
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

        public string ExchangeName { get; set; } = "bitget";

        public string ExchangeUrl { get; set; } = "https://api.bitget.com";

        public bool Alive { get; set; }
        public string ApiKey { get; set; }
        public string SecretKey { get; set; }
        public string PassPhrase { get; set; }

        private HMACSHA256 __encryptor = null;

        public HMACSHA256 Encryptor
        {
            get
            {
                if (__encryptor == null)
                    __encryptor = new HMACSHA256(Encoding.UTF8.GetBytes(this.SecretKey));

                return __encryptor;
            }
        }

        public string CreateSignature(HttpClient client, string endpoint, Dictionary<string, string> args = null)
        {
            var _timestamp = CUnixTime.NowMilli;
            var _query_string = mainXchg.ToQueryString2(args);

            var _sign_data = $"{_timestamp}GET{endpoint}{_query_string}";
            var _sign_hash = Encryptor.ComputeHash(Encoding.UTF8.GetBytes(_sign_data));

            var _signature = Convert.ToBase64String(_sign_hash);
            return _signature;
        }

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
                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrl}/api/spot/v1/public/products");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jarray = JsonConvert.DeserializeObject<CoinInfor>(_jstring);

                    var _queue_info = this.mainXchg.GetXInfors(ExchangeName);

                    foreach (var s in _jarray.data)
                    {
                        if (s.quoteCoin == "USDT" || s.quoteCoin == "USDC" || s.quoteCoin == "BTC")
                        {
                            _queue_info.symbols.Add(new QueueSymbol
                            {
                                symbol = s.symbolName,
                                compName = s.baseCoin,
                                baseName = s.baseCoin,
                                quoteName = s.quoteCoin,

                                dispName = s.symbol,
                                makerFee = s.makerFeeRate,
                                takerFee= s.takerFeeRate                                
                            });
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 4301);
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
                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrl}/api/spot/v1/public/currencies");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jarray = JsonConvert.DeserializeObject<CoinState>(_jstring);

                    foreach (var c in _jarray.data)
                    {
                        var _state = tickers.states.SingleOrDefault(x => x.currency == c.coinName);
                        if (_state == null)
                        {
                            _state = new WState
                            {
                                currency = c.coinName,
                                active = c.transfer,
                                deposit = c.transfer,
                                withdraw = c.transfer,
                                networks = new List<WNetwork>()
                            };

                            tickers.states.Add(_state);
                        }
                        else
                        {
                            _state.active = c.transfer;
                            _state.deposit = c.transfer;
                            _state.withdraw = c.transfer;
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

                        foreach (var n in c.chains)
                        {
                            var _name = c.coinName + "-" + n.chain;

                            var _network = _state.networks.SingleOrDefault(x => x.name == _name);
                            if (_network == null)
                            {
                                _network = new WNetwork
                                {
                                    name = _name,
                                    network = c.coinName,
                                    chain = n.chain,

                                    deposit = n.rechargeable,
                                    withdraw = n.withdrawable,

                                    withdrawFee = n.withdrawFee + n.extraWithDrawFee,
                                    minWithdrawal = n.minWithdrawAmount,
                                    
                                    minConfirm = n.depositConfirm
                                };

                                _state.networks.Add(_network);
                            }
                            else
                            {
                                _network.deposit = n.rechargeable;
                                _network.withdraw = n.withdrawable;
                            }
                        }
                    }

                    _result = true;
                }

                this.mainXchg.OnMessageEvent(ExchangeName, $"checking deposit & withdraw status...", 4302);
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 4303);
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
                    using HttpResponseMessage _response = await _wc.GetAsync($"{ExchangeUrl}/api/spot/v1/market/tickers");
                    var _jstring = await _response.Content.ReadAsStringAsync();
                    var _jarray = JsonConvert.DeserializeObject<RaTickers>(_jstring, mainXchg.JsonSettings);

                    for (var i = 0; i < tickers.items.Count; i++)
                    {
                        var _ticker = tickers.items[i];
                        if (_ticker.symbol == "X")
                            continue;

                        var _jitem = _jarray.data.SingleOrDefault(x => x.symbol == _ticker.symbol);
                        if (_jitem != null)
                        {
                            var _last_price = _jitem.close;
                            {
                                var _ask_price = _jitem.sellOne;
                                var _bid_price = _jitem.buyOne;

                                if (_ticker.quoteName == "USDT" || _ticker.quoteName == "USDC")
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

                            var _volume = _jitem.quoteVol;
                            {
                                var _prev_volume24h = _ticker.previous24h;
                                var _next_timestamp = _ticker.timestamp + 60 * 1000;

                                if (_ticker.quoteName == "USDT" || _ticker.quoteName == "USDC")
                                    _volume *= tickers.exchgRate;
                                else if (_ticker.quoteName == "BTC")
                                    _volume *= mainXchg.fiat_btc_price;

                                _ticker.volume24h = Math.Floor(_volume / mainXchg.Volume24hBase);

                                var _curr_timestamp = _jitem.ts;
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
                            this.mainXchg.OnMessageEvent(ExchangeName, $"not found: {_ticker.symbol}", 4304);
                            _ticker.symbol = "X";
                        }
                    }
                }

                _result = true;
            }
            catch (Exception ex)
            {
                this.mainXchg.OnMessageEvent(ExchangeName, ex, 4305);
            }

            return _result;
        }

        public ValueTask<bool> GetBookTickers(Tickers tickers)
        {
            throw new NotImplementedException();
        }

        public ValueTask<bool> GetTickers(Tickers tickers)
        {
            throw new NotImplementedException();
        }

        public ValueTask<decimal> GetPrice(string symbol)
        {
            throw new NotImplementedException();
        }

        public ValueTask<bool> GetVolumes(Tickers tickers)
        {
            throw new NotImplementedException();
        }
    }
}