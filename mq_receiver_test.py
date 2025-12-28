#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
MQ消息接收测试工具
用于验证虚拟机发送的消息是否成功到达宿主机
"""

import socket
import struct
import json
import sys
from datetime import datetime

class MQReceiver:
    def __init__(self, host='0.0.0.0', port=5678):
        self.host = host
        self.port = port
        self.socket = None
        self.stats = {
            'daily': {'count': 0, 'bytes': 0, 'last_time': None},
            'realtime': {'count': 0, 'bytes': 0, 'last_time': None},
            'ex_rights': {'count': 0, 'bytes': 0, 'last_time': None},
            'market_table': {'count': 0, 'bytes': 0, 'last_time': None}
        }
    
    def start(self):
        """启动接收器"""
        try:
            self.socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            self.socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            self.socket.bind((self.host, self.port))
            self.socket.listen(5)
            print(f"[{datetime.now()}] MQ接收器已启动，监听 {self.host}:{self.port}")
            print("等待虚拟机连接...")
            print("="*60)
            
            while True:
                conn, addr = self.socket.accept()
                print(f"[{datetime.now()}] 收到连接: {addr[0]}:{addr[1]}")
                
                # 处理连接
                self.handle_connection(conn, addr)
                
        except KeyboardInterrupt:
            print("\n正在关闭接收器...")
            self.print_statistics()
        except Exception as e:
            print(f"错误: {e}")
        finally:
            if self.socket:
                self.socket.close()
    
    def handle_connection(self, conn, addr):
        """处理单个连接"""
        try:
            while True:
                # 读取消息长度（4字节）
                length_data = self.recv_all(conn, 4)
                if not length_data:
                    break
                
                message_length = struct.unpack('>I', length_data)[0]
                
                # 读取队列名称长度（4字节）
                queue_name_length_data = self.recv_all(conn, 4)
                if not queue_name_length_data:
                    break
                
                queue_name_length = struct.unpack('>I', queue_name_length_data)[0]
                
                # 读取队列名称
                queue_name_data = self.recv_all(conn, queue_name_length)
                if not queue_name_data:
                    break
                
                queue_name = queue_name_data.decode('utf-8')
                
                # 读取JSON数据
                json_length = message_length - 4 - 4 - queue_name_length
                json_data = self.recv_all(conn, json_length)
                if not json_data:
                    break
                
                # 解析JSON
                try:
                    data = json.loads(json_data.decode('utf-8'))
                    self.process_message(queue_name, data, message_length)
                except json.JSONDecodeError as e:
                    print(f"[{datetime.now()}] JSON解析失败: {e}")
                    print(f"队列名称: {queue_name}")
                    print(f"数据长度: {json_length}")
                
        except Exception as e:
            print(f"[{datetime.now()}] 处理连接时出错: {e}")
        finally:
            conn.close()
            print(f"[{datetime.now()}] 连接已关闭: {addr[0]}:{addr[1]}")
    
    def recv_all(self, sock, n):
        """接收指定数量的字节"""
        data = b''
        while len(data) < n:
            chunk = sock.recv(n - len(data))
            if not chunk:
                return None
            data += chunk
        return data
    
    def process_message(self, queue_name, data, message_length):
        """处理接收到的消息"""
        record_count = 0
        if 'records' in data:
            record_count = len(data['records'])
        
        # 更新统计
        queue_type = self.get_queue_type(queue_name)
        if queue_type:
            self.stats[queue_type]['count'] += record_count
            self.stats[queue_type]['bytes'] += message_length
            self.stats[queue_type]['last_time'] = datetime.now()
            
            # 显示接收信息
            print(f"[{datetime.now()}] ✓ 收到消息 | 队列: {queue_name} | 记录数: {record_count} | 大小: {message_length}字节")
            
            # 显示第一条记录的示例（可选）
            if record_count > 0 and 'records' in data:
                first_record = data['records'][0]
                if queue_type == 'daily':
                    print(f"  示例: {first_record.get('stock_code', 'N/A')} | {first_record.get('trade_date', 'N/A')}")
                elif queue_type == 'realtime':
                    print(f"  示例: {first_record.get('stock_code', 'N/A')} | 价格: {first_record.get('new_price', 'N/A')}")
                elif queue_type == 'market_table':
                    print(f"  示例: {first_record.get('stock_code', 'N/A')} | {first_record.get('stock_name', 'N/A')}")
            
            # 每100条记录显示一次统计
            if self.stats[queue_type]['count'] % 100 == 0:
                self.print_statistics()
    
    def get_queue_type(self, queue_name):
        """根据队列名称判断数据类型"""
        if 'daily' in queue_name:
            return 'daily'
        elif 'realtime' in queue_name:
            return 'realtime'
        elif 'ex_rights' in queue_name:
            return 'ex_rights'
        elif 'market_table' in queue_name or 'code_table' in queue_name:
            return 'market_table'
        return None
    
    def print_statistics(self):
        """打印统计信息"""
        print("\n" + "="*60)
        print("当前统计:")
        for queue_type, stats in self.stats.items():
            if stats['count'] > 0:
                print(f"  {queue_type}: {stats['count']}条记录, {stats['bytes']}字节, "
                      f"最后接收: {stats['last_time']}")
        print("="*60 + "\n")

if __name__ == '__main__':
    # 默认监听所有接口的5678端口
    host = '0.0.0.0'
    port = 5678
    
    if len(sys.argv) > 1:
        port = int(sys.argv[1])
    
    print("="*60)
    print("MQ消息接收测试工具")
    print("用于验证VirtualBox虚拟机发送的消息")
    print("="*60)
    print()
    
    receiver = MQReceiver(host, port)
    receiver.start()

