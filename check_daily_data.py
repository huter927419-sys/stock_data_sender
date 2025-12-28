#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
检查日线数据是否在推送
用于验证接收到的消息中是否包含日线数据
"""

import json
import sys
from datetime import datetime

def check_message_for_daily_data(message_file=None):
    """检查消息中是否包含日线数据"""
    
    print("=" * 70)
    print("日线数据检查工具")
    print("=" * 70)
    print()
    
    # 如果提供了文件，从文件读取
    if message_file:
        try:
            with open(message_file, 'r', encoding='utf-8') as f:
                data = json.load(f)
            print(f"从文件读取: {message_file}")
        except Exception as e:
            print(f"读取文件失败: {e}")
            return
    else:
        # 从标准输入读取
        print("请粘贴接收到的JSON消息（按Ctrl+D结束输入）:")
        print()
        try:
            content = sys.stdin.read()
            data = json.loads(content)
        except EOFError:
            print("未输入数据")
            return
        except json.JSONDecodeError as e:
            print(f"JSON解析失败: {e}")
            return
    
    print()
    print("=" * 70)
    print("消息分析结果")
    print("=" * 70)
    print()
    
    # 检查队列名称
    if 'queue_name' in data:
        queue_name = data['queue_name']
        print(f"队列名称: {queue_name}")
        
        if 'daily' in queue_name.lower():
            print("✓ 这是日线数据队列")
        else:
            print(f"⚠ 这不是日线数据队列（日线数据队列通常包含 'daily'）")
    else:
        print("⚠ 未找到队列名称字段")
    
    print()
    
    # 检查数据记录
    if 'records' in data:
        records = data['records']
        print(f"记录数量: {len(records)}")
        
        if len(records) > 0:
            first_record = records[0]
            print()
            print("第一条记录:")
            print(json.dumps(first_record, indent=2, ensure_ascii=False))
            print()
            
            # 检查日线数据特征字段
            daily_data_fields = ['stock_code', 'trade_date', 'open_price', 'high_price', 
                                'low_price', 'close_price', 'volume', 'amount']
            
            found_fields = []
            missing_fields = []
            
            for field in daily_data_fields:
                if field in first_record:
                    found_fields.append(field)
                else:
                    missing_fields.append(field)
            
            print("日线数据字段检查:")
            if found_fields:
                print(f"  ✓ 找到字段: {', '.join(found_fields)}")
            if missing_fields:
                print(f"  ✗ 缺少字段: {', '.join(missing_fields)}")
            
            # 判断是否是日线数据
            if len(found_fields) >= 5:  # 至少找到5个日线数据字段
                print()
                print("=" * 70)
                print("✓ 确认：这是日线数据")
                print("=" * 70)
                
                # 显示数据示例
                if 'stock_code' in first_record:
                    print(f"股票代码: {first_record['stock_code']}")
                if 'trade_date' in first_record:
                    print(f"交易日期: {first_record['trade_date']}")
                if 'close_price' in first_record:
                    print(f"收盘价: {first_record['close_price']}")
            else:
                print()
                print("=" * 70)
                print("⚠ 警告：可能不是日线数据（缺少关键字段）")
                print("=" * 70)
        else:
            print("⚠ 记录列表为空")
    else:
        print("⚠ 未找到 'records' 字段")
        print()
        print("消息结构:")
        print(json.dumps(data, indent=2, ensure_ascii=False)[:500])
    
    print()

if __name__ == '__main__':
    if len(sys.argv) > 1:
        # 从文件读取
        check_message_for_daily_data(sys.argv[1])
    else:
        # 从标准输入读取
        check_message_for_daily_data()

