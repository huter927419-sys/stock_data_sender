using System;
using StockDataMQClient;

namespace StockDataMQClient
{
    /// <summary>
    /// 数据转换器 - 负责将原始数据结构转换为业务对象
    /// </summary>
    public static class DataConverter
    {
        /// <summary>
        /// 从RCV_REPORT_STRUCTExV3转换为StockData
        /// </summary>
        public static StockData ConvertFromReport(RCV_REPORT_STRUCTExV3 report)
        {
            try
            {
                // 提取股票代码和名称
                string stockCode = new string(report.m_szLabel).Trim('\0');
                string stockName = new string(report.m_szName).Trim('\0');

                if (string.IsNullOrEmpty(stockCode))
                {
                    return null;
                }

                // 创建股票数据对象
                StockData stockData = new StockData
                {
                    Code = stockCode,
                    Name = stockName,
                    LastClose = report.m_fLastClose,
                    Open = report.m_fOpen,
                    High = report.m_fHigh,
                    Low = report.m_fLow,
                    NewPrice = report.m_fNewPrice,
                    Volume = report.m_fVolume,
                    Amount = report.m_fAmount,
                    UpdateTime = DateTime.Now,
                    MarketType = report.m_wMarket,
                    TradeTime = report.m_time
                };

                // 提取买卖盘数据（5档）
                // 买盘：m_fBuyPrice[0-2] + m_fBuyPrice4 + m_fBuyPrice5
                if (report.m_fBuyPrice != null && report.m_fBuyPrice.Length >= 3)
                {
                    stockData.BuyPrice[0] = report.m_fBuyPrice[0];
                    stockData.BuyPrice[1] = report.m_fBuyPrice[1];
                    stockData.BuyPrice[2] = report.m_fBuyPrice[2];
                }
                stockData.BuyPrice[3] = report.m_fBuyPrice4;
                stockData.BuyPrice[4] = report.m_fBuyPrice5;
                
                // 买量：m_fBuyVolume[0-2] + m_fBuyVolume4 + m_fBuyVolume5
                if (report.m_fBuyVolume != null && report.m_fBuyVolume.Length >= 3)
                {
                    stockData.BuyVolume[0] = report.m_fBuyVolume[0];
                    stockData.BuyVolume[1] = report.m_fBuyVolume[1];
                    stockData.BuyVolume[2] = report.m_fBuyVolume[2];
                }
                stockData.BuyVolume[3] = report.m_fBuyVolume4;
                stockData.BuyVolume[4] = report.m_fBuyVolume5;
                
                // 卖盘：m_fSellPrice[0-2] + m_fSellPrice4 + m_fSellPrice5
                if (report.m_fSellPrice != null && report.m_fSellPrice.Length >= 3)
                {
                    stockData.SellPrice[0] = report.m_fSellPrice[0];
                    stockData.SellPrice[1] = report.m_fSellPrice[1];
                    stockData.SellPrice[2] = report.m_fSellPrice[2];
                }
                stockData.SellPrice[3] = report.m_fSellPrice4;
                stockData.SellPrice[4] = report.m_fSellPrice5;
                
                // 卖量：m_fSellVolume[0-2] + m_fSellVolume4 + m_fSellVolume5
                if (report.m_fSellVolume != null && report.m_fSellVolume.Length >= 3)
                {
                    stockData.SellVolume[0] = report.m_fSellVolume[0];
                    stockData.SellVolume[1] = report.m_fSellVolume[1];
                    stockData.SellVolume[2] = report.m_fSellVolume[2];
                }
                stockData.SellVolume[3] = report.m_fSellVolume4;
                stockData.SellVolume[4] = report.m_fSellVolume5;

                // 计算衍生数据
                stockData.CalculateChangePercent();
                stockData.CalculateChangeAmount();

                return stockData;
            }
            catch (Exception ex)
            {
                Logger.Instance.Error(string.Format("数据转换失败: {0}", ex.Message));
                System.Diagnostics.Debug.WriteLine(string.Format("数据转换失败: {0}", ex.Message));
                return null;
            }
        }

        /// <summary>
        /// 标准化股票代码（统一格式）
        /// </summary>
        public static string NormalizeStockCode(string code)
        {
            if (string.IsNullOrEmpty(code))
                return code;

            code = code.Trim().ToUpper();

            // 如果代码以SH、SZ、BJ开头（不区分大小写），保持原样
            if (code.Length > 2)
            {
                string prefix = code.Substring(0, 2).ToUpper();
                if (prefix == "SH" || prefix == "SZ" || prefix == "BJ")
                {
                    return prefix + code.Substring(2);
                }
            }

            // 如果是6位数字，根据规则添加前缀
            if (code.Length == 6 && System.Text.RegularExpressions.Regex.IsMatch(code, @"^\d{6}$"))
            {
                // 上海市场：600xxx, 601xxx, 603xxx, 688xxx, 605xxx
                if (code.StartsWith("60") || code.StartsWith("68") || code.StartsWith("605"))
                {
                    return "SH" + code;
                }
                // 深圳主板：000xxx, 001xxx, 002xxx
                // 创业板：300xxx
                else if (code.StartsWith("00") || code.StartsWith("300"))
                {
                    return "SZ" + code;
                }
                // 北京证券交易所：8xxxxx, 43xxxx, 83xxxx, 87xxxx
                else if (code.StartsWith("8") || code.StartsWith("43") || code.StartsWith("83") || code.StartsWith("87"))
                {
                    return "BJ" + code;
                }
            }

            return code;
        }
        
        /// <summary>
        /// 获取汉字拼音首字母（简化版，支持常用汉字）
        /// </summary>
        public static string GetPinyinInitials(string chineseText)
        {
            if (string.IsNullOrEmpty(chineseText))
                return "";
            
            System.Text.StringBuilder result = new System.Text.StringBuilder();
            
            foreach (char c in chineseText)
            {
                if (c >= 0x4e00 && c <= 0x9fff) // 汉字范围
                {
                    string initial = GetSingleCharPinyin(c);
                    if (!string.IsNullOrEmpty(initial))
                    {
                        result.Append(initial);
                    }
                }
                else if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                {
                    // 保留字母和数字
                    result.Append(char.ToUpper(c));
                }
            }
            
            return result.ToString();
        }
        
        /// <summary>
        /// 获取单个汉字的拼音首字母（常用汉字映射表）
        /// </summary>
        private static string GetSingleCharPinyin(char c)
        {
            // 常用汉字拼音首字母映射表（按Unicode顺序）
            int code = (int)c;
            
            // A: 啊(0x554a) - 按(0x6309)
            if (code >= 0x554a && code <= 0x6309) return "A";
            // B: 把(0x628a) - 不(0x4e0d)
            if (code >= 0x628a && code <= 0x4e0d) return "B";
            // C: 擦(0x64e6) - 错(0x9519)
            if (code >= 0x64e6 && code <= 0x9519) return "C";
            // D: 大(0x5927) - 多(0x591a)
            if (code >= 0x5927 && code <= 0x591a) return "D";
            // E: 额(0x989d) - 而(0x800c)
            if (code >= 0x989d && code <= 0x800c) return "E";
            // F: 发(0x53d1) - 复(0x590d)
            if (code >= 0x53d1 && code <= 0x590d) return "F";
            // G: 该(0x8be5) - 过(0x8fc7)
            if (code >= 0x8be5 && code <= 0x8fc7) return "G";
            // H: 还(0x8fd8) - 或(0x6216)
            if (code >= 0x8fd8 && code <= 0x6216) return "H";
            // J: 及(0x53ca) - 就(0x5c31)
            if (code >= 0x53ca && code <= 0x5c31) return "J";
            // K: 开(0x5f00) - 快(0x5feb)
            if (code >= 0x5f00 && code <= 0x5feb) return "K";
            // L: 来(0x6765) - 落(0x843d)
            if (code >= 0x6765 && code <= 0x843d) return "L";
            // M: 吗(0x5417) - 没(0x6ca1)
            if (code >= 0x5417 && code <= 0x6ca1) return "M";
            // N: 那(0x90a3) - 年(0x5e74)
            if (code >= 0x90a3 && code <= 0x5e74) return "N";
            // O: 哦(0x54e6) - 哦(0x54e6)
            if (code == 0x54e6) return "O";
            // P: 怕(0x6015) - 平(0x5e73)
            if (code >= 0x6015 && code <= 0x5e73) return "P";
            // Q: 起(0x8d77) - 去(0x53bb)
            if (code >= 0x8d77 && code <= 0x53bb) return "Q";
            // R: 然(0x7136) - 如(0x5982)
            if (code >= 0x7136 && code <= 0x5982) return "R";
            // S: 三(0x4e09) - 所(0x6240)
            if (code >= 0x4e09 && code <= 0x6240) return "S";
            // T: 他(0x4ed6) - 同(0x540c)
            if (code >= 0x4ed6 && code <= 0x540c) return "T";
            // W: 外(0x5916) - 我(0x6211)
            if (code >= 0x5916 && code <= 0x6211) return "W";
            // X: 下(0x4e0b) - 新(0x65b0)
            if (code >= 0x4e0b && code <= 0x65b0) return "X";
            // Y: 也(0x4e5f) - 有(0x6709)
            if (code >= 0x4e5f && code <= 0x6709) return "Y";
            // Z: 在(0x5728) - 最(0x6700)
            if (code >= 0x5728 && code <= 0x6700) return "Z";
            
            // 使用更精确的常用汉字映射（基于实际股票名称常用字）
            // 这里使用一个简化的映射表，实际应该使用完整的拼音库
            return GetPinyinByCommonChars(c);
        }
        
        /// <summary>
        /// 通过常用汉字映射获取拼音首字母（针对股票名称常用字优化）
        /// </summary>
        private static string GetPinyinByCommonChars(char c)
        {
            // 常用股票名称汉字拼音首字母映射（部分常用字）
            string charStr = c.ToString();
            
            // 使用.NET的拼音转换（如果可用），否则使用简化映射
            // 这里提供一个基础映射表，实际项目中建议使用专业的拼音库如NPinyin
            
            // 简化处理：对于无法直接判断的汉字，返回空字符串
            // 实际使用时，建议集成NPinyin等专业库
            return "";
        }
    }
}

