using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;


namespace StockDataMQClient
{

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct RCV_REPORT_STRUCTExV3
    {
        public UInt16 m_cbSize;                                    // �ṹ��С
        public Int32 m_time;                                       // ����ʱ��
        public UInt16 m_wMarket;                                   // ��Ʊ�г�����
        //m_szLabel��m_szName �Ķ�������Լ�ϲ�ÿ��Զ����
        [MarshalAs(   UnmanagedType.ByValArray, SizeConst=10)]   
        public   char[]  m_szLabel; //   ����,��'\0'��β   �����СΪSTKLABEL_LEN����c++������Ϊchar[10]     
        [MarshalAs(   UnmanagedType.ByValArray,   SizeConst=32)]   
        public   char[]  m_szName;  //   ����,��'\0'��β   �����СΪSTKNAME_LEN,��c++������Ϊchar[32]     
        /*  Ҳ���Զ����
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst = 10)]
        public string m_szLabel;                               // ����,��'\0'��β  �����СΪSTKLABEL_LEN����c++������Ϊchar[10]     
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst = 32)]
        public string m_szName;                                // ����,��'\0'��β ���СΪSTKNAME_LEN,��c++������Ϊchar[32]    
        */
        public Single m_fLastClose;                           // ����
        public Single m_fOpen;                                // ��
        public Single m_fHigh;                                // ���
        public Single m_fLow;                                 // ���
        public Single m_fNewPrice;                            // ����
        public Single m_fVolume;                              // �ɽ���
        public Single m_fAmount;                              // �ɽ���
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public Single[] m_fBuyPrice;                         // �����1,2,3
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public Single[] m_fBuyVolume;                        // ������1,2,3
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public Single[] m_fSellPrice;                        // ������1,2,3
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public Single[] m_fSellVolume;                       // ������1,2,3
        public Single m_fBuyPrice4;                          // �����4
        public Single m_fBuyVolume4;                         // ������4
        public Single m_fSellPrice4;                         // ������4
        public Single m_fSellVolume4;                        // ������4
        public Single m_fBuyPrice5;                          // �����5
        public Single m_fBuyVolume5;                         // ������5
        public Single m_fSellPrice5;                         // ������5
        public Single m_fSellVolume5;                        // ������5
    };


    //[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct RCV_FILE_HEADEx
    {
        public int m_dwAttrib;                      // �ļ�������
        public int m_dwLen;                         // �ļ�����
        public int m_dwSerialNoorTime;              //�ļ����кŻ�ʱ��.
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 260)]
        public char[] m_szFileName;                        // �ļ��� or URL
    }
    //[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi , Pack=1)]
    public struct RCV_DATA
    {
        public int m_wDataType;                     // �ļ�����
        public int m_nPacketNum;                    // ��¼��,�μ�עһ
        public RCV_FILE_HEADEx m_File;          // �ļ��ӿ�
        public int m_bDISK;                        // �ļ��Ƿ��Ѵ��̵��ļ�
        public IntPtr m_pData;
    } ;


    //��������ͷ  
    //��������ߡ���ʱ�����ж����õ�
    public struct RCV_EKE_HEADEx
    {
        public uint m_dwHeadTag; // = EKE_HEAD_TAG  
        public ushort m_wMarket; // �г�����  
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
        public char[] m_szLabel; // ��Ʊ����  
    }

    //������������  
    //[StructLayout(LayoutKind.Explicit)]
    public struct RCV_HISTORY_STRUCTEx
    {
        public int m_time;
        public Single m_fOpen; //����  
        public Single m_fHigh; //���  
        public Single m_fLow; //���  
        public Single m_fClose; //����  
        public Single m_fVolume; //��  
        public Single m_fAmount; //��  
        public UInt16 m_wAdvance; //����,��������Ч  
        public UInt16 m_wDecline; //����,��������Ч  
    }
  
    //������ʷ�����K������,ÿһ���ݽṹ��Ӧͨ�� m_time == EKE_HEAD_TAG,�ж��Ƿ�Ϊ m_head,Ȼ����������
    public struct RCV_HISMINUTE_STRUCTEx
    {
        public int m_time;              //UCT
        public Single m_fOpen;          //����
        public Single m_fHigh;          //���
        public Single m_fLow;           //���
        public Single m_fClose;         //����
        public Single m_fVolume;        //��
        public Single m_fAmount;        //��
        public Single m_fActiveBuyVol;  //����������û�м���m_fActiveBuyVol=0
    }

    //�����ʱ������
    public struct RCV_MINUTE_STRUCTEx
    {
        public Int32 m_time; //UCT
        public Single m_fPrice;
        public Single m_fVolume;
        public Single m_fAmount;
    }

    //�����Ȩ����
    public struct RCV_POWER_STRUCTEx
    {
        public int m_time;         //UCT
        public Single m_fGive;      //ÿ����
        public Single m_fPei;       //ÿ����
        public Single m_fPeiPrice;  //��ɼ�,���� m_fPei!=0.0f ʱ��Ч
        public Single m_fProfit;    //ÿ�ɺ���
    }


    //�ֱ�����///////////////////////////////////
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)] 
    public struct RCV_FENBI_STRUCTEx
    {
	    public int		m_lTime;				// hhmmss ����93056 ����9:
	    public Single	    m_fHigh;				// ���
	    public Single		m_fLow;					// ��� 
	    public Single		m_fNewPrice;			// ���� 
	    public Single		m_fVolume;				// �ɽ���
	    public Single		m_fAmount;				// �ɽ���
	    public int		m_lStroke;				// ����

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public Single[]     m_fBuyPrice;            // �����1,2,3,4,5
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
	    public Single[]		m_fBuyVolume;			// ������1,2,3,4,5
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
	    public Single[]		m_fSellPrice;			// ������1,2,3,4,5
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
	    public Single[]		m_fSellVolume;			// ������1,2,3,4,5

    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)] 
    public struct RCV_FENBI
    {
	    public UInt16 		m_wMarket;					// ��Ʊ�г�����
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
	    public char[]		m_szLabel;	                // ��Ʊ����,��'\0'��β
	    public Int32		m_lDate;					// �ֱ����ݵ����� FORMAT:
	    public Single		m_fLastClose;				// ����
	    public Single		m_fOpen;					// ��
	    public UInt16		m_nCount;					//m_Data���������ֱʱ���
        public IntPtr       m_Data;						//����Ϊm_nCount
    };


    //�������
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct  RCV_TABLE_STRUCT
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
        public char[] m_szLabel;            //��Ʊ����,��'\0'��β,�� "500500"
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public char[] m_szName;             //��Ʊ����,��'\0'��β,"��ָ֤��"
        public UInt16 m_cProperty;          //ÿ�ֹ���
    }

    //�������ͷ�ṹ
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct HLMarketType
    {
        public UInt16 m_wMarket;     //�г�����
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public char[] m_Name;        //�г�����
        public int m_lProperty;      //�г����ԣ�δ���壩
        public int m_lDate;          //�������ڣ�20030114��
        public UInt16 m_PeriodCount; //����ʱ�θ���
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public UInt16[] m_OpenTime;  //����ʱ�� 1,2,3,4,5
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
        public UInt16[] m_CloseTime; //����ʱ�� 1,2,3,4,5
        public UInt16 m_nCount;      //���г���֤ȯ����
        public IntPtr m_Data;        //����Ϊm_nCount
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct Fin_LJF_STRUCTEx
    {
        public UInt16 m_wMarket;         // ��Ʊ�г�����
        public UInt16 N1;                // �����ֶ�
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
        public char[] m_szLabel;         // ��Ʊ����,��'\0'��β,�� "600050"  10���ֽ� ͬͨ�ӹ淶����
        public int BGRQ;                 // �������ݵ����� ����걨 ������ �� 20090630 ��ʾ 2009����걨
        public Single ZGB;             // �ܹɱ�
        public Single GJG;             // ���ҹ�
        public Single FQFRG;           // �����˷��˹�
        public Single FRG;             // ���˹�
        public Single BGS;             // B��
        public Single HGS;             // H��
        public Single MQLT;            // Ŀǰ��ͨ
        public Single ZGG;             // ְ����
        public Single A2ZPG;           // A2ת���
        public Single ZZC;             // ���ʲ�(ǧԪ)
        public Single LDZC;            // �����ʲ�
        public Single GDZC;            // �̶��ʲ�
        public Single WXZC;            // �����ʲ�
        public Single CQTZ;            // ����Ͷ��
        public Single LDFZ;            // ������ծ
        public Single CQFZ;            // ���ڸ�ծ
        public Single ZBGJJ;           // �ʱ�������
        public Single MGGJJ;           // ÿ�ɹ�����
        public Single GDQY;            // �ɶ�Ȩ��
        public Single ZYSR;            // ��Ӫ����
        public Single ZYLR;            // ��Ӫ����
        public Single QTLR;            // ��������
        public Single YYLR;            // Ӫҵ����
        public Single TZSY;            // Ͷ������
        public Single BTSR;            // ��������
        public Single YYWSZ;           // Ӫҵ����֧
        public Single SNSYTZ;          // �����������
        public Single LRZE;            // �����ܶ�
        public Single SHLR;            // ˰������
        public Single JLR;             // ������
        public Single WFPLR;           // δ��������
        public Single MGWFP;           // ÿ��δ����
        public Single MGSY;            // ÿ������
        public Single MGJZC;           // ÿ�ɾ��ʲ�
        public Single TZMGJZC;         // ����ÿ�ɾ��ʲ�
        public Single GDQYB;           // �ɶ�Ȩ���
        public Single JZCSYL;          // ����������
    }

    class StockDrv
    {
        public const int FILE_HISTORY_EX = 2;//����������
        public const int FILE_MINUTE_EX = 4;//������������
        public const int FILE_POWER_EX = 6;//����Ȩ����
        public const int FILE_5MINUTE_EX = 81;//�������������
        public const int FILE_BASE_EX = 0x1000;//Ǯ�����ݻ��������ļ���m_szFileName�������ļ���
        public const int FILE_NEWS_EX = 0x1002;//�����࣬��������m_szFileName����Ŀ¼������
        public const int RCV_WORK_SENDMSG = 4;//������ʽ���Ͷ��壬������Ϣ��ʽ
        public const int RCV_MSG_STKDATA = 0x8001;//ָ��ʹ�õ���Ϣ
        public const int RCV_REPORT = 0x3f001234;//��Ʊ����
        public const int RCV_FILEDATA = 0x3f001235;//�ļ�
        public const int RCV_FENBIDATA= 0x3f001258; //�ֱ����ݣ����������ݾ�Ϊ��ֵ
        public const int RCV_MKTTBLDATA = 0x3f001259;//���յ��г��������
        public const int RCV_FINANCEDATA = 0x3f001300;//���յ������ļ�����
        public const UInt32 EKE_HEAD_TAG = 0xffffffff;//����ͷ�ṹ���

    }
}
