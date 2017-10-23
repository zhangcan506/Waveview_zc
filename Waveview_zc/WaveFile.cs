using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Drawing;

namespace  Waveview_zc
{
    public class WaveFile
    {
        /// <summary>
        /// 文件字节数组  整个wav文件的字节流数组
        /// </summary>
        private byte[] _waveFile;

        /// <summary>
        /// 文件标识符串  4b
        /// </summary>
        public string fileId { get; set; }

        /// <summary>
        /// 头后文件长度  4b  文件长度-8
        /// </summary>
        public UInt32 fileLen { get; set; }

        /// <summary>
        /// 波形文件标识符 4b
        /// </summary>
        public string waveId { get; set; }

        /// <summary>
        /// 格式块标识符串 4b
        /// </summary>
        public string chkId { get; set; }

        /// <summary>
        /// 头后块长度   4b
        /// </summary>
        public UInt32 chkLen { get; set; }

        /// <summary>
        /// 格式标记    2b
        /// </summary>
        public UInt16 wFormatTag { get; set; }

        /// <summary>
        /// 声道数 2b
        /// </summary>
        public UInt16 wChannels { get; set; }

        /// <summary>
        /// 采样率 4b
        /// </summary>
        public UInt32 dwSampleRate { get; set; }

        /// <summary>
        /// 平均字节率 4b
        /// </summary>
        public UInt32 dwAvgBytesRate { get; set; }

        /// <summary>
        /// 数据块对齐   2b
        /// </summary>
        public UInt16 wBlockAlign { get; set; }

        /// <summary>
        /// 采样位数    2b
        /// </summary>
        public UInt16 wBitsPerSample { get; set; }

        /// <summary>
        /// 扩展域大小   2b
        /// </summary>
        public UInt16 wExtSize { get; set; }

        /// <summary>
        /// 扩展域 wExtSizeb
        /// </summary>
        public byte[] extraInfo { get; set; }

        /// <summary>
        /// 数据块标识符串 4b
        /// </summary>
        public string dchkId { get; set; }

        /// <summary>
        /// 头后块长度 4b
        /// </summary>
        public UInt32 dchkLen { get; set; }

        /// <summary>
        /// 主数据段   真正的数据块 字节流数组 可能包括 左右波形，也可能只有一个声道
        /// </summary>
        public byte[] wData { get; set; }

        /// <summary>
        /// 左波形采样数据
        /// </summary>
        public byte[] wXl { get; set; }

        /// <summary>
        /// 右波形采样数据
        /// </summary>
        public byte[] wXr { get; set; }

        public UInt32 zhangcanlength { get; set; }
        /// <summary>
        /// 构造函数
        /// </summary>
        public WaveFile(byte[] waveFile)
        {
            this._waveFile = waveFile;  //音频文件
            this.fileId = Array2String(0, 4);  //表头（Header） 4字节   固定为"RIFF".
            this.fileLen = Array2Uint32(4, 4); //               4字节   little-endian 32-bit 正整数   长度
            this.waveId = Array2String(8, 4); //                4字节   WAV文件标志（WAVE）
            this.chkId = Array2String(12, 4); //                4字节   波形格式标志（fmt ），最后一位空格
            this.chkLen = Array2Uint32(16, 4); //               4字节   过滤字节（一般为00000010H）
            this.wFormatTag = Array2Uint16(20, 2); //           2字节    格式种类（值为1时，表示数据为线性PCM编码）
            this.wChannels = Array2Uint16(22, 2); //            2字节    通道数，单声道为1，双声道为2
            this.dwSampleRate = Array2Uint32(24, 4); //         4字节    采样频率
            this.dwAvgBytesRate = Array2Uint32(28, 4); //       4字节    波形数据传输速率（每秒平均字节数）每秒所需字节数     
            this.wBlockAlign = Array2Uint16(32, 2); //          2字节    DATA数据块长度，字节。一个样本的长度    数据块对齐单位(每个采样需要的字节数) 
            this.wBitsPerSample = Array2Uint16(34, 2); //       2字节    PCM位宽      每个采样需要的bit数 
            this.wExtSize = Array2Uint16(36, 2); //             2字节   长度
            this.extraInfo = new byte[wExtSize]; //             wExtSize字节     数据内容
            Array.Copy(this._waveFile, 38, this.extraInfo, 0, wExtSize);
            int sIndex = FindData(38, _waveFile.Length); //// 寻找data块位置  “data”出现的首位置 sIndex
            this.dchkId = Array2String(sIndex, 4); //           4字节    数据块ID
            this.dchkLen = Array2Uint32(sIndex + 4, 4); //      4字节    数据块长度
            this.wData = new byte[_waveFile.Length - (sIndex + 9)]; //纯数据块
            Array.Copy(this._waveFile, sIndex + 8, wData, 0, wData.Length); //
        }

        #region 读取用辅助方法
        /// <summary>
        /// 字节数组返回字符串
        /// </summary>
        private string Array2String(int start, int length)
        {
            byte[] _temp = new byte[length];
            Array.Copy(this._waveFile, start, _temp, 0, length);
            return Encoding.UTF8.GetString(_temp);
        }

        /// <summary>
        /// 字节数组返回数字
        /// </summary>
        private UInt16 Array2Uint16(int start, int length)
        {
            byte[] _temp = new byte[length];
            Array.Copy(this._waveFile, start, _temp, 0, length);
            return BitConverter.ToUInt16(_temp, 0);
        }

        /// <summary>
        /// 字节数组返回数字
        /// </summary>
        private UInt32 Array2Uint32(int start, int length)
        {
            byte[] _temp = new byte[length];
            Array.Copy(this._waveFile, start, _temp, 0, length);
            return BitConverter.ToUInt32(_temp, 0);
        }

        /// <summary>
        /// 寻找data块位置
        /// </summary>
        private int FindData(int start, int end)
        {
            byte[] _find = new byte[4];
            int result = 0;
            for (int i = start; i < end - 4; i += 2)
            {
                Array.Copy(this._waveFile, i, _find, 0, 4);
                if (Encoding.UTF8.GetString(_find).ToLower() == "data")
                {
                    result = i;
                    break;
                }
            }
            return result;
        }
        #endregion

        /// <summary>
        /// 把读好的数据写文件
        /// </summary>
        private void CreateWaveFile()
        {
            using (FileStream fs = new FileStream("E:\\test.wav", FileMode.OpenOrCreate))
            {
                BinaryWriter bf = new BinaryWriter(fs);
                fs.Write(this._waveFile, 0, 36);
                bf.Write((short)0);
                bf.Write(Encoding.UTF8.GetBytes("fact"));
                bf.Write(this.wData.Length);
                fs.Write(this.wData, 0, this.wData.Length);
                bf.Close();
                bf.Dispose();
            }
        }


      
    }
}
