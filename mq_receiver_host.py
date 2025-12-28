#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
MQ消息接收程序（宿主机端）
用于接收从虚拟机发送的MQ消息
"""

import socket
import struct
import json
import sys
from datetime import datetime

class MQReceiverHost:
    def __init__(self, host='0.0.0.0', port=5678):
        """
        初始化接收器
        :param host: 监听地址，0.0.0.0表示监听所有网络接口
        :param port: 监听端口
        """
        self.host = host
        self.port = port
        self.socket = None
        self.running = False
        self.total_messages = 0
        self.total_bytes = 0
        self.connections = 0
    
    def start(self):
        """启动接收器"""
        try:
            # 创建TCP套接字
            self.socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            self.socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            self.socket.bind((self.host, self.port))
            self.socket.listen(5)
            
            print("=" * 70)
            print("MQ消息接收程序（宿主机端）")
            print("=" * 70)
            print(f"监听地址: {self.host}:{self.port}")
            print(f"等待虚拟机连接...")
            print("=" * 70)
            print()
            
            self.running = True
            
            while self.running:
                try:
                    # 接受连接
                    conn, addr = self.socket.accept()
                    self.connections += 1
                    print(f"[{datetime.now()}] ✓ 收到新连接: {addr[0]}:{addr[1]} (总连接数: {self.connections})")
                    
                    # 处理连接
                    self.handle_connection(conn, addr)
                    
                except OSError:
                    # 套接字关闭时退出
                    break
                except Exception as e:
                    print(f"[{datetime.now()}] ✗ 接受连接时出错: {e}")
                    
        except KeyboardInterrupt:
            print("\n\n正在关闭接收器...")
        except Exception as e:
            print(f"\n✗ 启动失败: {e}")
        finally:
            self.stop()
    
    def handle_connection(self, conn, addr):
        """处理单个连接"""
        try:
            while True:
                # 读取消息长度（4字节，大端序）
                length_data = self.recv_all(conn, 4)
                if not length_data:
                    break
                
                message_length = struct.unpack('>I', length_data)[0]
                
                # 读取队列名称长度（4字节，大端序）
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
                
                # 解析并处理消息
                self.process_message(queue_name, json_data, message_length)
                
        except socket.timeout:
            print(f"[{datetime.now()}] ⚠ 接收超时: {addr[0]}:{addr[1]}")
        except Exception as e:
            print(f"[{datetime.now()}] ✗ 处理连接时出错 ({addr[0]}:{addr[1]}): {e}")
        finally:
            conn.close()
            print(f"[{datetime.now()}] 连接已关闭: {addr[0]}:{addr[1]}")
            print()
    
    def recv_all(self, sock, n):
        """接收指定数量的字节"""
        data = b''
        while len(data) < n:
            try:
                chunk = sock.recv(n - len(data))
                if not chunk:
                    return None
                data += chunk
            except socket.timeout:
                return None
            except Exception:
                return None
        return data
    
    def process_message(self, queue_name, json_data_bytes, message_length):
        """处理接收到的消息"""
        try:
            # 解析JSON
            json_str = json_data_bytes.decode('utf-8')
            data = json.loads(json_str)
            
            # 更新统计
            self.total_messages += 1
            self.total_bytes += message_length
            
            # 获取记录数
            record_count = 0
            if isinstance(data, dict) and 'records' in data:
                record_count = len(data['records'])
            elif isinstance(data, list):
                record_count = len(data)
            
            # 显示接收信息
            print(f"[{datetime.now()}] ✓ 收到消息")
            print(f"   队列名称: {queue_name}")
            print(f"   记录数量: {record_count}")
            print(f"   消息大小: {message_length} 字节")
            
            # 显示数据摘要
            if record_count > 0:
                if isinstance(data, dict) and 'records' in data:
                    first_record = data['records'][0]
                    print(f"   第一条记录: {self.format_record(first_record)}")
                elif isinstance(data, list):
                    print(f"   第一条记录: {self.format_record(data[0])}")
            
            # 显示统计信息
            print(f"   累计接收: {self.total_messages} 条消息, {self.total_bytes} 字节")
            print()
            
            # 如果是测试消息，显示详细信息
            if isinstance(data, dict) and data.get('type') == 'test':
                print("   [测试消息] 内容:")
                for i, record in enumerate(data.get('records', [])[:3]):  # 只显示前3条
                    print(f"      {i+1}. {record}")
                if len(data.get('records', [])) > 3:
                    print(f"      ... 还有 {len(data.get('records', [])) - 3} 条记录")
                print()
            
        except json.JSONDecodeError as e:
            print(f"[{datetime.now()}] ✗ JSON解析失败: {e}")
            print(f"   队列名称: {queue_name}")
            print(f"   数据长度: {len(json_data_bytes)} 字节")
            print(f"   数据预览: {json_data_bytes[:100]}...")
            print()
        except Exception as e:
            print(f"[{datetime.now()}] ✗ 处理消息时出错: {e}")
            print()
    
    def format_record(self, record):
        """格式化记录显示"""
        if isinstance(record, dict):
            # 显示前几个字段
            items = list(record.items())[:3]
            return ", ".join([f"{k}={v}" for k, v in items])
        else:
            return str(record)[:100]
    
    def stop(self):
        """停止接收器"""
        self.running = False
        if self.socket:
            try:
                self.socket.close()
            except:
                pass
        
        print()
        print("=" * 70)
        print("接收器已关闭")
        print(f"总计接收: {self.total_messages} 条消息, {self.total_bytes} 字节")
        print(f"总计连接: {self.connections} 次")
        print("=" * 70)

def main():
    """主函数"""
    # 默认配置
    host = '0.0.0.0'  # 监听所有网络接口
    port = 5678
    
    # 解析命令行参数
    if len(sys.argv) > 1:
        port = int(sys.argv[1])
    if len(sys.argv) > 2:
        host = sys.argv[2]
    
    # 创建并启动接收器
    receiver = MQReceiverHost(host, port)
    
    try:
        receiver.start()
    except KeyboardInterrupt:
        print("\n\n程序被用户中断")
    except Exception as e:
        print(f"\n程序异常退出: {e}")

if __name__ == '__main__':
    print()
    print("正在启动MQ消息接收程序...")
    print("按 Ctrl+C 可以停止程序")
    print()
    
    main()

