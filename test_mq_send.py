#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
MQ消息发送测试工具
用于测试从虚拟机发送消息到宿主机
"""

import socket
import struct
import json
import sys
from datetime import datetime

class MQTestSender:
    def __init__(self, host='10.0.2.2', port=5678):
        self.host = host
        self.port = port
        self.socket = None
    
    def connect(self):
        """连接到MQ服务器"""
        try:
            self.socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            self.socket.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)  # 禁用Nagle算法
            self.socket.settimeout(5)  # 5秒超时
            self.socket.connect((self.host, self.port))
            print(f"[{datetime.now()}] ✓ 连接成功: {self.host}:{self.port}")
            return True
        except socket.timeout:
            print(f"[{datetime.now()}] ✗ 连接超时: {self.host}:{self.port}")
            return False
        except ConnectionRefusedError:
            print(f"[{datetime.now()}] ✗ 连接被拒绝: {self.host}:{self.port}")
            print("   请确认宿主机上的接收器已启动 (运行 mq_receiver_test.py)")
            return False
        except Exception as e:
            print(f"[{datetime.now()}] ✗ 连接失败: {e}")
            return False
    
    def send_test_message(self, queue_name="test_queue", message_count=1):
        """发送测试消息"""
        if not self.socket:
            print("未连接，请先调用 connect()")
            return False
        
        try:
            # 创建测试数据
            test_records = []
            for i in range(message_count):
                test_records.append({
                    "test_id": i + 1,
                    "test_time": datetime.now().isoformat(),
                    "test_message": f"这是第 {i + 1} 条测试消息",
                    "source": "虚拟机测试工具"
                })
            
            # 构建JSON数据
            json_data = json.dumps({
                "type": "test",
                "records": test_records,
                "timestamp": datetime.now().isoformat()
            }, ensure_ascii=False)
            
            # 构建消息：消息长度(4字节) + 队列名称长度(4字节) + 队列名称 + JSON数据
            queue_name_bytes = queue_name.encode('utf-8')
            data_bytes = json_data.encode('utf-8')
            
            message_length = 4 + 4 + len(queue_name_bytes) + len(data_bytes)
            message = bytearray()
            
            # 写入消息总长度（大端序）
            message.extend(struct.pack('>I', message_length))
            
            # 写入队列名称长度（大端序）
            message.extend(struct.pack('>I', len(queue_name_bytes)))
            
            # 写入队列名称
            message.extend(queue_name_bytes)
            
            # 写入JSON数据
            message.extend(data_bytes)
            
            # 发送消息
            self.socket.sendall(bytes(message))
            print(f"[{datetime.now()}] ✓ 发送成功: {message_count} 条测试消息到队列 '{queue_name}'")
            print(f"   消息大小: {len(message)} 字节")
            print(f"   数据内容: {json_data[:100]}..." if len(json_data) > 100 else f"   数据内容: {json_data}")
            return True
            
        except Exception as e:
            print(f"[{datetime.now()}] ✗ 发送失败: {e}")
            return False
    
    def close(self):
        """关闭连接"""
        if self.socket:
            self.socket.close()
            self.socket = None
            print(f"[{datetime.now()}] 连接已关闭")

def main():
    print("=" * 60)
    print("MQ消息发送测试工具")
    print("用于测试从虚拟机发送消息到宿主机")
    print("=" * 60)
    print()
    
    # 默认配置
    host = '10.0.2.2'
    port = 5678
    queue_name = "test_queue"
    message_count = 1
    
    # 解析命令行参数
    if len(sys.argv) > 1:
        host = sys.argv[1]
    if len(sys.argv) > 2:
        port = int(sys.argv[2])
    if len(sys.argv) > 3:
        message_count = int(sys.argv[3])
    
    print(f"配置:")
    print(f"  目标地址: {host}:{port}")
    print(f"  队列名称: {queue_name}")
    print(f"  消息数量: {message_count}")
    print()
    
    sender = MQTestSender(host, port)
    
    # 连接
    if not sender.connect():
        print()
        print("连接失败，请检查:")
        print("  1. 宿主机上的接收器是否已启动 (运行 mq_receiver_test.py)")
        print("  2. 网络连接是否正常")
        print("  3. 防火墙是否允许连接")
        return
    
    print()
    
    # 发送测试消息
    success = sender.send_test_message(queue_name, message_count)
    
    print()
    if success:
        print("=" * 60)
        print("测试完成！")
        print("请在宿主机上查看接收器是否收到消息")
        print("=" * 60)
    else:
        print("=" * 60)
        print("测试失败！")
        print("=" * 60)
    
    # 关闭连接
    sender.close()

if __name__ == '__main__':
    try:
        main()
    except KeyboardInterrupt:
        print("\n\n测试已取消")
    except Exception as e:
        print(f"\n发生错误: {e}")

