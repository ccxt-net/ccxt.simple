namespace CCXT.Simple.Exchanges.Kucoin
{
    /// <summary>
    /// https://www.kucoin.com/_pxapi/pool-currency/currencies
    /// </summary>
    public class CoinState
    {
        public bool success { get; set; }
        public int code { get; set; }
        public string msg { get; set; }
        public long timestamp { get; set; }
        public List<Currency> data { get; set; }
    }

    public class Currency
    {
        public string currencyType { get; set; }
        public decimal innerWithdrawMinFee { get; set; }
        public bool isDebitEnabled { get; set; }
        public string withdrawDisabledTip { get; set; }
        public int precision { get; set; }
        public string orgAddress { get; set; }
        public decimal makerFeeCoefficient { get; set; }
        public int type { get; set; }
        public bool isDepositEnabled { get; set; }
        public decimal withdrawMinSize { get; set; }
        public string txUrl { get; set; }
        public string userAddressName { get; set; }
        public long createdAt { get; set; }
        public int feeCategory { get; set; }
        public string currencyName { get; set; }
        public bool isDisplayDeposit { get; set; }
        public string currency { get; set; }
        public decimal takerFeeCoefficient { get; set; }
        public string iconUrl { get; set; }
        public bool isDisplayWithdraw { get; set; }
        public decimal withdrawMinFee { get; set; }
        public string chainName { get; set; }
        public bool isDigital { get; set; }
        public int walletPrecision { get; set; }
        public string chainFullName { get; set; }
        public string depositDisabledTip { get; set; }
        public int confirmationCount { get; set; }
        public string name { get; set; }
        public bool isWithdrawEnabled { get; set; }
        public bool isMarginEnabled { get; set; }
        public bool isNft { get; set; }
        public bool isNftGiftable { get; set; }
        public string preWithdrawTipEnabled { get; set; }
        public string preDepositTip { get; set; }
        public string preWithdrawTip { get; set; }
        public string preDepositTipEnabled { get; set; }
        public string isChainEnabled { get; set; }
        public string withdrawFeeRate { get; set; }
        public string chainOrder { get; set; }
        public string status { get; set; }
        public string contractAddress { get; set; }
        public string withdrawRemark { get; set; }
        public string showStakingRemark { get; set; }
        public string isTransferEnabled { get; set; }
        public string showDepositRemark { get; set; }
        public string depositRemark { get; set; }
        public string showWithdrawRemark { get; set; }
        public string coinType { get; set; }
        public string depositMinSize { get; set; }
    }
 }