﻿using Newtonsoft.Json.Linq;

namespace CCXT.Simple.Exchanges.Bithumb
{
    /// <summary>
    /// https://www.bithumb.com/withdraw_address/coincodeinfo 
    /// </summary>
    public class CoinState
    {
        public int error { get; set; }
        public string message { get; set; }
        public List<CsData> data { get; set; }
    }

    public class CsData
    {
        public string coinTypeCd { get; set; }
        public string coinSymbolNm { get; set; }
        public string coinNmKr { get; set; }
        public string coinNmEn { get; set; }
        public string networkType { get; set; }
        public string outAvailableYn { get; set; }
        public string scndAddrYn { get; set; }
        public string scndAddrNmKr { get; set; }
        public string scndAddrNmEn { get; set; }
    }

    /// <summary>
    /// https://api.bithumb.com/public/assetsstatus/ALL
    /// </summary>
    public class CoinAsset
    {
        public int status { get; set; }
        public JObject data { get; set; }
    }

    public class CaData
    {
        public int withdrawal_status { get; set; }
        public int deposit_status { get; set; }
    }
}