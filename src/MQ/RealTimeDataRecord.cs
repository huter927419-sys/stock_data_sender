using System;

namespace StockDataMQClient
{
    /// <summary>
    /// 实时数据记录
    /// </summary>
    public class RealTimeDataRecord
    {
        public string StockCode { get; set; }
        public string StockName { get; set; }
        public ushort MarketCode { get; set; }
        public DateTime UpdateTime { get; set; }
        public int TimeStamp { get; set; }
        
        // 价格数据
        public decimal LastClose { get; set; }      // 昨收
        public decimal Open { get; set; }            // 开盘
        public decimal High { get; set; }            // 最高
        public decimal Low { get; set; }             // 最低
        public decimal NewPrice { get; set; }        // 最新价
        
        // 成交数据
        public decimal Volume { get; set; }           // 成交量
        public decimal Amount { get; set; }          // 成交额
        
        // 买卖盘数据
        public decimal[] BuyPrice { get; set; }      // 买盘1,2,3,4,5
        public decimal[] BuyVolume { get; set; }     // 买量1,2,3,4,5
        public decimal[] SellPrice { get; set; }     // 卖盘1,2,3,4,5
        public decimal[] SellVolume { get; set; }    // 卖量1,2,3,4,5
        
        public RealTimeDataRecord()
        {
            BuyPrice = new decimal[5];
            BuyVolume = new decimal[5];
            SellPrice = new decimal[5];
            SellVolume = new decimal[5];
        }
    }
}

